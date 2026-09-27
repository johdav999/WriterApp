# Replace placeholders using docs/native-authentication.md. These are public registration values, not secrets.
# Dot-source the edited local copy in the terminal used to launch the desktop app.
$env:WRITERAPP_AUTH_TENANT_ID = '<customer-tenant-guid>'
$env:WRITERAPP_AUTH_AUTHORITY = 'https://<tenant-subdomain>.ciamlogin.com/'
$env:WRITERAPP_AUTH_CLIENT_ID = '<desktop-public-client-guid>'
$env:WRITERAPP_AUTH_REDIRECT_URI = 'http://localhost'
$env:WRITERAPP_AUTH_SCOPES = 'api://<api-application-client-guid>/access_as_user'
$env:WRITERAPP_API_BASE_URL = 'https://<staging-app-host>/'
