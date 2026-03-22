using System;

namespace WriterApp.Client.State
{
    public sealed record GlobalSearchNavigationTarget(
        Guid DocumentId,
        Guid SectionId,
        Guid? PageId,
        string EntityType,
        string EntityId,
        string Query);

    public sealed class GlobalSearchNavigationService
    {
        private GlobalSearchNavigationTarget? _pendingTarget;

        public event Action<GlobalSearchNavigationTarget?>? Changed;

        public GlobalSearchNavigationTarget? PendingTarget => _pendingTarget;

        public void SetPending(GlobalSearchNavigationTarget target)
        {
            _pendingTarget = target;
            Changed?.Invoke(_pendingTarget);
        }

        public bool TryConsume(Guid documentId, Guid sectionId, out GlobalSearchNavigationTarget? target)
        {
            if (_pendingTarget is not null
                && _pendingTarget.DocumentId == documentId
                && _pendingTarget.SectionId == sectionId)
            {
                target = _pendingTarget;
                _pendingTarget = null;
                Changed?.Invoke(null);
                return true;
            }

            target = null;
            return false;
        }

        public void Clear()
        {
            if (_pendingTarget is null)
            {
                return;
            }

            _pendingTarget = null;
            Changed?.Invoke(null);
        }
    }
}
