using System.Diagnostics;
using Instrumentation;
using InstrumentationInterface;
using Microsoft.AspNetCore.Mvc;
using PharmaGo.UsersService.IBusinessLogic;
using PharmaGo.UsersService.Models.In;
using PharmaGo.UsersService.Models.Out;

namespace PharmaGo.UsersService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly ILoginManager _loginManager;
        private readonly ICustomMetrics _customMetrics;
        private readonly IStructuredLogger _structuredLogger;

       public LoginController(ILoginManager manager, ICustomMetrics customMetrics, IStructuredLogger structuredLogger)
       {
            _loginManager = manager;
            _customMetrics = customMetrics;
            _structuredLogger = structuredLogger;
       }

        [HttpPost]
        public IActionResult Login([FromBody] LoginModelRequest userModel)
        {
            using var activity = PharmaGoActivitySource.Source.StartActivity("users.login", ActivityKind.Internal);
            activity?.SetTag("pharmago.operation", "login");

            _customMetrics.LoginInvocations();
            
            try
            {
                var authorization = _loginManager.Login(userModel.UserName, userModel.Password);
                
                _structuredLogger.LogInformation(
                    "Login succeeded",
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "login_success",
                        ["component"] = "LoginController",
                        ["operation"] = "login",
                        ["outcome"] = "success",
                        ["user_id"] = authorization.UserId
                    });
                
                return Ok(new LoginModelResponse() { token = authorization.Token, role = authorization.Role, userName = authorization.UserName });
            }
            catch (Exception ex)
            {
                _structuredLogger.LogWarning(
                    "Login failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["pharma_biz"] = "login_fail",
                        ["component"] = "LoginController",
                        ["operation"] = "login",
                        ["outcome"] = "failed",
                        ["user_provided"] = !string.IsNullOrWhiteSpace(userModel.UserName)
                    });
                
                throw;
            }
        }

    }
}

