namespace Barebones.MasterServer
{
    public interface IAccountsDatabaseAccessor
    {
        /// <summary>
        /// Should create an empty object with account data.
        /// </summary>
        /// <returns></returns>
        IAccountInfoData CreateAccountInstance();
        /// <summary>
        /// Gets user account from database
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        IAccountInfoData GetAccountByUsername(string username);
        /// <summary>
        /// Gets user account from database by token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        IAccountInfoData GetAccountByToken(string token);
        /// <summary>
        /// Gets user account from database by email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        IAccountInfoData GetAccountByEmail(string email);
        /// <summary>
        /// Saves code that user gets when reset pasword request
        /// </summary>
        /// <param name="account"></param>
        /// <param name="code"></param>
        void SavePasswordResetCode(IAccountInfoData account, string code);
        /// <summary>
        /// Get data for password reset
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        IPasswordResetData GetPasswordResetData(string email);
        /// <summary>
        /// Email confirmation code user gets after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        void SaveEmailConfirmationCode(string email, string code);
        /// <summary>
        /// Get email confirmation code for user after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        string GetEmailConfirmationCode(string email);
        /// <summary>
        /// Update all account information in database
        /// </summary>
        /// <param name="account"></param>
        void UpdateAccount(IAccountInfoData account);
        /// <summary>
        /// Create new account in database
        /// </summary>
        /// <param name="account"></param>
        void InsertNewAccount(IAccountInfoData account);
        /// <summary>
        /// Insert account token to database
        /// </summary>
        /// <param name="account"></param>
        /// <param name="token"></param>
        void InsertToken(IAccountInfoData account, string token);
    }
}