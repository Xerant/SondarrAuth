using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sondarr.Auth.Shared.Services;

namespace Sondarr.Auth.Shared.Attributes
{
    /// <summary>
    /// Authorization attribute that requires the current user to have a specific role.
    /// This attribute can be applied to controllers or action methods to enforce role-based access control.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _requiredRole;

        /// <summary>
        /// Initializes a new instance of the RequireRoleAttribute class.
        /// </summary>
        /// <param name="requiredRole">The role that the user must have to access the resource.</param>
        public RequireRoleAttribute(string requiredRole)
        {
            _requiredRole = requiredRole ?? throw new ArgumentNullException(nameof(requiredRole));
        }

        /// <summary>
        /// Called early in the filter pipeline to confirm request is authorized.
        /// </summary>
        /// <param name="context">The authorization filter context.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userContextService = context.HttpContext.RequestServices.GetService(typeof(IUserContextService)) as IUserContextService;
            if (userContextService == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            if (!userContextService.IsAuthenticated())
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!userContextService.HasRole(_requiredRole))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }

    /// <summary>
    /// Authorization attribute that requires the current user to have any of the specified roles.
    /// This attribute can be applied to controllers or action methods to enforce role-based access control.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireAnyRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _requiredRoles;

        /// <summary>
        /// Initializes a new instance of the RequireAnyRoleAttribute class.
        /// </summary>
        /// <param name="requiredRoles">The roles that the user must have at least one of to access the resource.</param>
        public RequireAnyRoleAttribute(params string[] requiredRoles)
        {
            _requiredRoles = requiredRoles ?? throw new ArgumentNullException(nameof(requiredRoles));
            if (_requiredRoles.Length == 0)
            {
                throw new ArgumentException("At least one role must be specified.", nameof(requiredRoles));
            }
        }

        /// <summary>
        /// Called early in the filter pipeline to confirm request is authorized.
        /// </summary>
        /// <param name="context">The authorization filter context.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userContextService = context.HttpContext.RequestServices.GetService(typeof(IUserContextService)) as IUserContextService;
            if (userContextService == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            if (!userContextService.IsAuthenticated())
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!userContextService.HasAnyRole(_requiredRoles))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
