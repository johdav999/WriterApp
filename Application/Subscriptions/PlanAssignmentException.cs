using System;

namespace WriterApp.Application.Subscriptions
{
    public sealed class PlanAssignmentException : Exception
    {
        public PlanAssignmentException(PlanAssignmentErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        public PlanAssignmentErrorCode Code { get; }
    }

    public enum PlanAssignmentErrorCode
    {
        InvalidUserId,
        InvalidPlanKey,
        PlanNotFound,
        PlanInactive,
        AssignmentExists
    }
}
