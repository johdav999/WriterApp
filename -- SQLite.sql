-- SQLite
SELECT Id, UpdatedAt, UpdatedAtUnixSeconds
FROM Documents
ORDER BY UpdatedAtUnixSeconds DESC
LIMIT 5;
