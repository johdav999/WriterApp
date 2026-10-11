using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using WriterApp.Client.State;

namespace WriterApp.Client.Services
{
    public sealed class ClientLogoutService
    {
        private readonly NavigationManager _navigation;
        private readonly AuthStateService _authStateService;
        private readonly AuthMeStateService _authMeStateService;
        private readonly CurrentDocumentStateService _currentDocumentStateService;
        private readonly CurrentSceneStateService _currentSceneStateService;
        private readonly CurrentProjectStateService _currentProjectStateService;
        private readonly GlobalSearchNavigationService _globalSearchNavigationService;
        private readonly ProjectStructureCacheService _projectStructureCacheService;
        private readonly ProjectProgressCacheService _projectProgressCacheService;
        private readonly OnboardingOverlayStateService _onboardingOverlayStateService;
        private readonly LastOpenedDocumentStateService _lastOpenedDocumentStateService;
        private readonly AiCommandStatusService _aiCommandStatusService;
        private readonly WebBrainstormingSessions? _brainstorming;

        public ClientLogoutService(
            NavigationManager navigation,
            AuthStateService authStateService,
            AuthMeStateService authMeStateService,
            CurrentDocumentStateService currentDocumentStateService,
            CurrentSceneStateService currentSceneStateService,
            CurrentProjectStateService currentProjectStateService,
            GlobalSearchNavigationService globalSearchNavigationService,
            ProjectStructureCacheService projectStructureCacheService,
            ProjectProgressCacheService projectProgressCacheService,
            OnboardingOverlayStateService onboardingOverlayStateService,
            LastOpenedDocumentStateService lastOpenedDocumentStateService,
            AiCommandStatusService aiCommandStatusService, WebBrainstormingSessions? brainstorming = null)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _authStateService = authStateService ?? throw new ArgumentNullException(nameof(authStateService));
            _authMeStateService = authMeStateService ?? throw new ArgumentNullException(nameof(authMeStateService));
            _currentDocumentStateService = currentDocumentStateService ?? throw new ArgumentNullException(nameof(currentDocumentStateService));
            _currentSceneStateService = currentSceneStateService ?? throw new ArgumentNullException(nameof(currentSceneStateService));
            _currentProjectStateService = currentProjectStateService ?? throw new ArgumentNullException(nameof(currentProjectStateService));
            _globalSearchNavigationService = globalSearchNavigationService ?? throw new ArgumentNullException(nameof(globalSearchNavigationService));
            _projectStructureCacheService = projectStructureCacheService ?? throw new ArgumentNullException(nameof(projectStructureCacheService));
            _projectProgressCacheService = projectProgressCacheService ?? throw new ArgumentNullException(nameof(projectProgressCacheService));
            _onboardingOverlayStateService = onboardingOverlayStateService ?? throw new ArgumentNullException(nameof(onboardingOverlayStateService));
            _lastOpenedDocumentStateService = lastOpenedDocumentStateService ?? throw new ArgumentNullException(nameof(lastOpenedDocumentStateService));
            _aiCommandStatusService = aiCommandStatusService ?? throw new ArgumentNullException(nameof(aiCommandStatusService));
            _brainstorming = brainstorming;
        }

        public async Task ClearClientStateAsync()
        {
            _authStateService.Reset();
            if (_brainstorming is not null) {
                try { await _brainstorming.ClearReviewIdentityAsync(); }
                // The module revokes its tab grant before touching IndexedDB. Storage failure must not stop logout.
                catch (Microsoft.JSInterop.JSException) { }
            }
            _authMeStateService.Reset();
            _currentDocumentStateService.Clear();
            _currentSceneStateService.Clear();
            _currentProjectStateService.Clear();
            _globalSearchNavigationService.Clear();
            _projectStructureCacheService.Clear();
            _projectProgressCacheService.Clear();
            _onboardingOverlayStateService.Clear();
            _aiCommandStatusService.Clear();
            await _lastOpenedDocumentStateService.ClearAsync();
        }

        public async Task BeginLogoutAsync()
        {
            await ClearClientStateAsync();
            _navigation.NavigateTo("/app/logout", forceLoad: true);
        }
    }
}
