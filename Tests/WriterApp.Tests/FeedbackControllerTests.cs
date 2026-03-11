using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Feedback;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class FeedbackControllerTests
    {
        [Fact]
        public async Task Submit_ReturnsBadRequest_WhenDescriptionIsMissing()
        {
            FeedbackController controller = BuildController(isDevelopment: true);
            FeedbackController.FeedbackSubmitRequest request = new(
                "bug",
                "Missing description",
                null,
                false,
                null);

            IActionResult result = await controller.Submit(request, CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        }

        [Fact]
        public async Task Submit_ReturnsOk_WhenPayloadIsValid()
        {
            FeedbackController controller = BuildController(isDevelopment: true);
            FeedbackController.FeedbackSubmitRequest request = new(
                "enhancement",
                "Feedback subject",
                "Feedback description",
                false,
                null);

            IActionResult result = await controller.Submit(request, CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        }

        private static FeedbackController BuildController(bool isDevelopment)
        {
            FeedbackController controller = new(
                NullLogger<FeedbackController>.Instance,
                new StubUserIdResolver(),
                new StubFeedbackEmailSender(isDevelopment));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubFeedbackEmailSender : IFeedbackEmailSender
        {
            private readonly bool _isDevelopment;

            public StubFeedbackEmailSender(bool isDevelopment)
            {
                _isDevelopment = isDevelopment;
            }

            public Task<FeedbackEmailSendResult> SendAsync(FeedbackEmailRequest request, CancellationToken ct)
            {
                string message = _isDevelopment
                    ? "Feedback captured locally (Mailgun not configured)."
                    : "Feedback sent.";
                return Task.FromResult(FeedbackEmailSendResult.Success(message));
            }
        }
    }
}
