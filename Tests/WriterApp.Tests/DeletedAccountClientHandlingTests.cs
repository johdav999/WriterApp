using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Security;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DeletedAccountClientHandlingTests
    {
        [Fact]
        public async Task AuthStateService_AccountDeletedResponse_ReturnsDeletedState()
        {
            DeletedAccountStateService deletedState = new();
            DuplicateAccountStateService duplicateState = new();
            HttpClient http = new(new StubHttpMessageHandler(_ =>
                Task.FromResult(CreateDeletedResponse())))
            {
                BaseAddress = new Uri("http://localhost/")
            };

            AuthStateService service = new(http, NullLogger<AuthStateService>.Instance, deletedState, duplicateState);

            AuthState state = await service.GetAsync(forceRefresh: true);

            Assert.True(state.IsDeletedAccount);
            Assert.False(state.IsAuthenticated);
            Assert.True(deletedState.IsDeletedAccount);
        }

        [Fact]
        public async Task ApiUnauthorizedRedirectHandler_AccountDeletedResponse_RedirectsToDeletedAccountPage()
        {
            DeletedAccountStateService deletedState = new();
            DuplicateAccountStateService duplicateState = new();
            TestNavigationManager navigation = new("http://localhost/app/projects");
            ApiUnauthorizedRedirectHandler handler = new(
                NullLogger<ApiUnauthorizedRedirectHandler>.Instance,
                navigation,
                deletedState,
                duplicateState)
            {
                InnerHandler = new StubHttpMessageHandler(_ =>
                    Task.FromResult(CreateDeletedResponse()))
            };

            using HttpClient http = new(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            using HttpResponseMessage response = await http.GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(deletedState.IsDeletedAccount);
            Assert.EndsWith("/app/deleted-account", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AuthStateService_DuplicateAccountResponse_ReturnsDuplicateState()
        {
            DeletedAccountStateService deletedState = new();
            DuplicateAccountStateService duplicateState = new();
            HttpClient http = new(new StubHttpMessageHandler(_ =>
                Task.FromResult(CreateDuplicateResponse())))
            {
                BaseAddress = new Uri("http://localhost/")
            };

            AuthStateService service = new(http, NullLogger<AuthStateService>.Instance, deletedState, duplicateState);

            AuthState state = await service.GetAsync(forceRefresh: true);

            Assert.True(state.IsDuplicateAccount);
            Assert.False(state.IsAuthenticated);
            Assert.True(duplicateState.IsDuplicateAccount);
        }

        [Fact]
        public async Task ApiUnauthorizedRedirectHandler_DuplicateAccountResponse_RedirectsToDuplicateAccountPage()
        {
            DeletedAccountStateService deletedState = new();
            DuplicateAccountStateService duplicateState = new();
            TestNavigationManager navigation = new("http://localhost/app/projects");
            ApiUnauthorizedRedirectHandler handler = new(
                NullLogger<ApiUnauthorizedRedirectHandler>.Instance,
                navigation,
                deletedState,
                duplicateState)
            {
                InnerHandler = new StubHttpMessageHandler(_ =>
                    Task.FromResult(CreateDuplicateResponse()))
            };

            using HttpClient http = new(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            using HttpResponseMessage response = await http.GetAsync("/api/auth/me");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.True(duplicateState.IsDuplicateAccount);
            Assert.EndsWith("/app/duplicate-account", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        }

        private static HttpResponseMessage CreateDeletedResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "{\"code\":\"account_deleted\",\"message\":\"This Prosa account has been deleted. Sign out before registering again.\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        }

        private static HttpResponseMessage CreateDuplicateResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    "{\"code\":\"duplicate_account\",\"message\":\"An account may already exist for this email under a different sign-in method.\",\"currentLoginProvider\":\"externalid\",\"emailPresent\":true,\"maskedEmail\":\"j***n@gmail.com\",\"matchedUserIdMasked\":\"***abcd1234\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _handler(request);
        }

        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string initialUri)
            {
                Initialize("http://localhost/", initialUri);
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
                Uri = ToAbsoluteUri(uri).ToString();
            }

            protected override void NavigateToCore(string uri, NavigationOptions options)
            {
                Uri = ToAbsoluteUri(uri).ToString();
            }
        }
    }
}
