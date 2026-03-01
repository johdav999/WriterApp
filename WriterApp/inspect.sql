INSERT INTO UserPlanAssignments (
    UserId,
    PlanId,
    AssignedUtc,
    AssignedBy
)
VALUES (
    'DEV',
    '6D1D34EF-2A0F-4B24-8B3F-7F3F4A4B9F0B',
    CURRENT_TIMESTAMP,
    'seed'
);

SELECT * FROM Plans;
SELECT * FROM PlanEntitlements;
SELECT * FROM UserPlanAssignments;