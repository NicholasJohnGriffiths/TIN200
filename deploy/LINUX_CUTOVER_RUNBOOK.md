# Linux App Cutover Runbook

Target app: `tin200app-linux-260302144937`  
Resource group: `rg-tin200-260302144937`

## 1) Pre-cutover checklist (T-24h to T-1h)

- Confirm latest deploy workflow is green in GitHub Actions.
- Confirm app health endpoint returns 200:

```powershell
curl.exe -k -I https://tin200app-linux-260302144937.azurewebsites.net/health
```

- Confirm login page returns 200:

```powershell
curl.exe -k -I https://tin200app-linux-260302144937.azurewebsites.net/Login
```

- Confirm Email Events route redirects unauthenticated users to login (expected 302):

```powershell
curl.exe -k -I https://tin200app-linux-260302144937.azurewebsites.net/Survey/EmailEvents
```

- Confirm managed identity is enabled:

```powershell
az webapp identity show --resource-group rg-tin200-260302144937 --name tin200app-linux-260302144937 --query "{type:type,principalId:principalId}" -o table
```

- Confirm Email Events settings exist:

```powershell
az webapp config appsettings list --resource-group rg-tin200-260302144937 --name tin200app-linux-260302144937 --query "[?starts_with(name, 'EmailEvents')].[name,value]" -o table
```

- Confirm SQL connection is configured:

```powershell
az webapp config connection-string list --resource-group rg-tin200-260302144937 --name tin200app-linux-260302144937 -o table
```

## 2) Cutover checklist (T-0)

- Take database backup/snapshot.
- Communicate maintenance window and cutover point.
- Freeze risky/non-essential changes.
- Trigger deploy from `main` (or push approved commit).
- Wait for workflow `Deploy to Linux App Service` to complete successfully.
- Re-check:
	- `/health` => 200
	- `/Login` => 200
	- `/Survey/EmailEvents` => 302 (anonymous)

## 3) Post-cutover validation (T+0 to T+30m)

- Log in as admin and validate core pages.
- Validate create/read/update/delete flow for a known company record.
- Validate survey open/save path.
- Validate Email Events page while authenticated:
	- Rows load.
	- No banner saying CLI/token acquisition failed.
- Validate error logs are clean:

```powershell
az webapp log download --resource-group rg-tin200-260302144937 --name tin200app-linux-260302144937 --log-file live-logs-latest.zip
```

## 4) Rollback plan

- If severe issue appears after cutover:
	- Redeploy last known good commit from GitHub Actions.
	- If needed, re-apply known-good app settings:
		- `ConnectionStrings__DefaultConnection`
		- `EmailEvents__Enabled`
		- `EmailEvents__AzPath`
		- `EmailEvents__CommandTemplate`
	- Restart app:

```powershell
az webapp restart --resource-group rg-tin200-260302144937 --name tin200app-linux-260302144937
```

## 5) Hardening items (recommended this week)

- Set GitHub repository variable `EMAIL_EVENTS_COMMAND_TEMPLATE` to keep Email Events query configuration managed by CI/CD.
- Add Application Insights alert for HTTP 500 spikes.
- Add availability test for `/health` and `/Login`.
- Capture a short runbook dry-run in team notes.
