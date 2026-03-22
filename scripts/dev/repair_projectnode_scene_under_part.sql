SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now datetimeoffset = SYSUTCDATETIME();

DECLARE @Repairs TABLE
(
    ProjectId uniqueidentifier NOT NULL,
    PartId uniqueidentifier NOT NULL,
    SceneId uniqueidentifier NOT NULL,
    ChapterId uniqueidentifier NOT NULL
);

INSERT INTO @Repairs (ProjectId, PartId, SceneId, ChapterId)
SELECT
    scene.ProjectId,
    part.Id,
    scene.Id,
    NEWID()
FROM ProjectNodes scene
JOIN ProjectNodes part
    ON part.Id = scene.ParentId
WHERE scene.NodeType = 2
  AND part.NodeType = 0;

INSERT INTO ProjectNodes
(
    Id,
    ProjectId,
    ParentId,
    NodeType,
    Title,
    OrderIndex,
    LinkedSectionId,
    MetadataJson,
    WordCountCache,
    UpdatedUtc
)
SELECT
    repair.ChapterId,
    repair.ProjectId,
    repair.PartId,
    1,
    N'Chapter 1',
    0,
    NULL,
    NULL,
    0,
    @Now
FROM @Repairs repair;

UPDATE scene
SET
    scene.ParentId = repair.ChapterId,
    scene.OrderIndex = 0,
    scene.UpdatedUtc = @Now
FROM ProjectNodes scene
JOIN @Repairs repair
    ON repair.SceneId = scene.Id;

COMMIT TRANSACTION;

SELECT
    repair.ProjectId,
    part.Title AS PartTitle,
    chapter.Id AS ChapterId,
    chapter.Title AS ChapterTitle,
    scene.Id AS SceneId,
    scene.Title AS SceneTitle
FROM @Repairs repair
JOIN ProjectNodes part
    ON part.Id = repair.PartId
JOIN ProjectNodes chapter
    ON chapter.Id = repair.ChapterId
JOIN ProjectNodes scene
    ON scene.Id = repair.SceneId
ORDER BY repair.ProjectId;
