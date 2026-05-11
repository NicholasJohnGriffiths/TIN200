-- Migration 016: Add SurveyReminderEmailTemplate column to Config table
ALTER TABLE [Config]
ADD [SurveyReminderEmailTemplate] TEXT NULL;
