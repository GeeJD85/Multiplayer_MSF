using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class MsfAuthClient : MsfBaseClient
    {
        public delegate void SignInCallback(AccountInfoPacket accountInfo, string error);

        /// <summary>
        /// Check if user is signed in
        /// </summary>
        public bool IsSignedIn { get; protected set; }

        /// <summary>
        /// Check if user is now logging in
        /// </summary>
        public bool IsNowSigningIn { get; protected set; }

        /// <summary>
        /// Current useraccount info
        /// </summary>
        public AccountInfoPacket AccountInfo { get; protected set; }

        public event Action OnSignedInEvent;
        public event Action OnSignedUpEvent;
        public event Action OnSignedOutEvent;

        public MsfAuthClient(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Sends a registration request to server
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callback"></param>
        public void SignUp(Dictionary<string, string> data, SuccessCallback callback)
        {
            SignUp(data, callback, Connection);
        }

        /// <summary>
        /// Sends a registration request to given connection
        /// </summary>
        public void SignUp(Dictionary<string, string> data, SuccessCallback callback, IClientSocket connection)
        {
            if (IsNowSigningIn)
            {
                callback.Invoke(false, "Signing in is already in progress");
                return;
            }

            if (IsSignedIn)
            {
                callback.Invoke(false, "Already signed in");
                return;
            }

            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected to server");
                return;
            }

            // We first need to get an aes key 
            // so that we can encrypt our login data
            Msf.Security.GetAesKey(aesKey =>
            {
                if (aesKey == null)
                {
                    callback.Invoke(false, "Failed to register due to security issues");
                    return;
                }

                var encryptedData = Msf.Security.EncryptAES(data.ToBytes(), aesKey);

                connection.SendMessage((short)MsfMessageCodes.SignUpRequest, encryptedData, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        callback.Invoke(false, response.AsString("Unknown error"));
                        return;
                    }

                    callback.Invoke(true, null);

                    OnSignedUpEvent?.Invoke();
                });
            }, connection);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        public void SignOut()
        {
            SignOut(Connection);
        }

        /// <summary>
        /// Initiates a log out. In the process, disconnects and connects
        /// back to the server to ensure no state data is left on the server.
        /// </summary>
        public void SignOut(IClientSocket connection)
        {
            if (!IsSignedIn)
            {
                return;
            }

            IsSignedIn = false;
            AccountInfo = null;

            if ((connection != null) && connection.IsConnected)
            {
                connection.Reconnect();
            }

            OnSignedOutEvent?.Invoke();
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        /// <param name="callback"></param>
        public void SignInAsGuest(SignInCallback callback)
        {
            Logs.Debug("Signing in as Guest...");

            SignIn(new Dictionary<string, string>()
            {
                {"guest", string.Empty }
            }, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to log in as a guest
        /// </summary>
        public void SignInAsGuest(SignInCallback callback, IClientSocket connection)
        {
            SignIn(new Dictionary<string, string>()
            {
                {"guest", string.Empty }
            }, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignIn(string username, string password, SignInCallback callback, IClientSocket connection)
        {
            SignIn(new Dictionary<string, string>
            {
                {"username", username},
                {"password", password}
            }, callback, connection);
        }

        /// <summary>
        /// Sends a login request, using given credentials
        /// </summary>
        public void SignIn(string username, string password, SignInCallback callback)
        {
            SignIn(username, password, callback, Connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callback"></param>
        public void SignIn(Dictionary<string, string> data, SignInCallback callback)
        {
            SignIn(data, callback, Connection);
        }

        /// <summary>
        /// Sends a generic login request
        /// </summary>
        public void SignIn(Dictionary<string, string> data, SignInCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected to server");
                return;
            }

            IsNowSigningIn = true;

            // We first need to get an aes key 
            // so that we can encrypt our login data
            Msf.Security.GetAesKey(aesKey =>
            {
                if (aesKey == null)
                {
                    IsNowSigningIn = false;
                    callback.Invoke(null, "Failed to log in due to security issues");
                    return;
                }

                var encryptedData = Msf.Security.EncryptAES(data.ToBytes(), aesKey);

                connection.SendMessage((short)MsfMessageCodes.SignInRequest, encryptedData, (status, response) =>
                {
                    IsNowSigningIn = false;

                    if (status != ResponseStatus.Success)
                    {
                        callback.Invoke(null, response.AsString("Unknown error"));
                        return;
                    }

                    IsSignedIn = true;

                    AccountInfo = response.Deserialize(new AccountInfoPacket());

                    callback.Invoke(AccountInfo, null);

                    OnSignedInEvent?.Invoke();
                });
            }, connection);
        }

        /// <summary>
        /// Sends an e-mail confirmation code to the server
        /// </summary>
        /// <param name="code"></param>
        /// <param name="callback"></param>
        public void ConfirmEmail(string code, SuccessCallback callback)
        {
            ConfirmEmail(code, callback, Connection);
        }

        /// <summary>
        /// Sends an e-mail confirmation code to the server
        /// </summary>
        public void ConfirmEmail(string code, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected to server");
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(false, "You're not logged in");
                return;
            }

            connection.SendMessage((short)MsfMessageCodes.EmailConfirmationRequest, code, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to server, to ask for an e-mail confirmation code
        /// </summary>
        /// <param name="callback"></param>
        public void RequestEmailConfirmationCode(SuccessCallback callback)
        {
            RequestEmailConfirmationCode(callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to ask for an e-mail confirmation code
        /// </summary>
        public void RequestEmailConfirmationCode(SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected to server");
                return;
            }

            if (!IsSignedIn)
            {
                callback.Invoke(false, "You're not logged in");
                return;
            }

            connection.SendMessage((short)MsfMessageCodes.EmailConfirmationCodeRequest, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to server, to ask for a password reset
        /// </summary>
        public void RequestPasswordReset(string email, SuccessCallback callback)
        {
            RequestPasswordReset(email, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, to ask for a password reset
        /// </summary>
        public void RequestPasswordReset(string email, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected to server");
                return;
            }

            connection.SendMessage((short)MsfMessageCodes.PasswordResetCodeRequest, email, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a new password to server
        /// </summary>
        public void ChangePassword(PasswordChangeData data, SuccessCallback callback)
        {
            ChangePassword(data, callback, Connection);
        }

        /// <summary>
        /// Sends a new password to server
        /// </summary>
        public void ChangePassword(PasswordChangeData data, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected to server");
                return;
            }

            var dictionary = new Dictionary<string, string>()
            {
                {"email", data.Email },
                {"code", data.Code },
                {"password", data.NewPassword }
            };

            connection.SendMessage((short)MsfMessageCodes.ChangePasswordRequest, dictionary.ToBytes(), (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }
    }
}