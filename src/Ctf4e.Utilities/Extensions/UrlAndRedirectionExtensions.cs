using System;
using Microsoft.AspNetCore.Mvc;

namespace Ctf4e.Utilities.Extensions
{
    public static class UrlAndRedirectionExtensions
    {
        private static string GetControllerName<TController>() where TController : Controller
        {
            const string controllerNameSuffix = "Controller";

            string controllerName = typeof(TController).Name;
            if(controllerName.EndsWith(controllerNameSuffix))
                controllerName = controllerName.Substring(0, controllerName.Length - controllerNameSuffix.Length);

            return controllerName;
        }

        /// <summary>
        ///     Generates a URL for the given controller and action.
        /// </summary>
        /// <typeparam name="TController">Type of target controller.</typeparam>
        /// <param name="urlHelper">URL helper object.</param>
        /// <param name="actionMethodName">Name of target action method.</param>
        /// <param name="values">Optional. Action method arguments.</param>
        /// <exception cref="Exception">Thrown when the underlying <see cref="UrlHelperExtensions.Action(IUrlHelper, string, string, object)" /> call returns an empty string.</exception>
        /// <returns></returns>
        public static string Action<TController>(this IUrlHelper urlHelper, string actionMethodName, object values = null) where TController : Controller
        {
            string controllerName = GetControllerName<TController>();

            string url = urlHelper.Action(actionMethodName, controllerName, values);
            if(string.IsNullOrEmpty(url))
                throw new Exception($"Could not generate URL for controller \"{controllerName}\" and action method \"{actionMethodName}\".");

            return url;
        }

        /// <summary>
        ///     Redirects to the given controller and action.
        /// </summary>
        /// <typeparam name="TController">Type of target controller.</typeparam>
        /// <param name="controller">Source controller issuing the redirect.</param>
        /// <param name="actionMethodName">Name of target action method.</param>
        /// <param name="routeValues">Optional. Action method arguments.</param>
        /// <returns></returns>
        public static RedirectToActionResult RedirectToAction<TController>(this Controller controller, string actionMethodName, object routeValues = null) where TController : Controller
        {
            return controller.RedirectToAction(actionMethodName, GetControllerName<TController>(), routeValues);
        }
    }
}