namespace LtiAdvantageTools;

/// <summary>
/// Represents errors that occur during processing of an LTI login.
/// The error messages are tailored for use in a BadRequest (400) response.
/// </summary>
public class LtiLoginException(string message) : Exception(message);