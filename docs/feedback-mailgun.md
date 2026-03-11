# Feedback Email With Mailgun

The feedback form sends email through Mailgun using these configuration keys:

- `MailGunAPIKey`
- `MailGunBaseUrl`
- `MailGunDomain`
- `MailGunFromEmail`
- `MailGunFromName`
- `FeedbackToEmail`

No secrets should be committed to source control.

## Local development

Set environment variables before starting the app. PowerShell example:

```powershell
$env:MailGunAPIKey="key-..."
$env:MailGunBaseUrl="https://api.eu.mailgun.net"
$env:MailGunDomain="mg.example.com"
$env:MailGunFromEmail="noreply@example.com"
$env:MailGunFromName="Prosa Feedback"
$env:FeedbackToEmail="support@example.com"
```

`WebApplication.CreateBuilder(args)` reads environment variables automatically, so the same keys work locally without additional code changes.

## Azure App Service

Add these App Service environment variables / application settings:

- `MailGunAPIKey`
- `MailGunBaseUrl`
- `MailGunDomain`
- `MailGunFromEmail`
- `MailGunFromName`
- `FeedbackToEmail`

Azure App Service exposes these values as environment variables, and the app reads them through standard configuration.
