namespace SplitIt.Application.Constants
{
    /// <summary>
    /// Version of the legal documents (privacy policy + terms) the user accepts at registration.
    /// Bump it whenever the documents change so consent can be re-requested.
    /// </summary>
    public static class LegalConsent
    {
        public const string CurrentVersion = "1.0";
    }
}
