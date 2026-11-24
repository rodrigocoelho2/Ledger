/*
    EntryPlanDelete
    Deletes every instalment of a plan (past and future) of a person. Returns how many were deleted.
*/
CREATE PROCEDURE [dbo].[EntryPlanDelete]
    @UserUniqueID UNIQUEIDENTIFIER,
    @PlanKey      UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE e
    FROM [dbo].[Entry] e
    INNER JOIN [dbo].[User] u ON u.[UserID] = e.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
      AND e.[PlanKey] = @PlanKey;

    SELECT @@ROWCOUNT;
END
