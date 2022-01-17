using System;

namespace Ctf4e.Api.Exceptions
{
    public class CtfApiException : Exception
    {
        /// <summary>
        /// Debug information alongside with the HTTP response, if there is any.
        /// </summary>
        public string FormattedResponseContent { get; set; }

        public CtfApiException(string message, string formattedResponseContent)
            : base(message)
        {
            FormattedResponseContent = formattedResponseContent;
        }
    }
}