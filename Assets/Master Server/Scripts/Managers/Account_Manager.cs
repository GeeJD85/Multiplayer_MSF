using Aevien.UI;
using Barebones.MasterServer;
using Barebones.Networking;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GW.MasterServer
{
    public class Account_Manager : BaseClientModule
    {
        private string outputMessage = string.Empty;

        private SignIn_View signinView;
        private SignUp_View signupView;
        private PasswordReset_View passwordResetView;
        private PasswordResetCode_View passwordResetCodeView;
        private EmailConfirmation_View emailConfirmationView;

        [Header("Editor Debug Settings"), SerializeField]
        private string defaultUsername = "qwerty";
        [SerializeField]
        private string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        private string defaultPassword = "qwerty123!@#";
        [SerializeField]
        private bool useDefaultCredentials = false;

        public UnityEvent OnSignedInEvent;
        public UnityEvent OnSignedOutEvent;
        public UnityEvent OnEmailConfirmedEvent;
        public UnityEvent OnPasswordChangedEvent;

        protected override void Initialize()
        {
            signinView = ViewsManager.GetView<SignIn_View>("SigninView");
            signupView = ViewsManager.GetView<SignUp_View>("SignupView");
            passwordResetView = ViewsManager.GetView<PasswordReset_View>("PasswordResetView");
            passwordResetCodeView = ViewsManager.GetView<PasswordResetCode_View>("PasswordResetCodeView");
            emailConfirmationView = ViewsManager.GetView<EmailConfirmation_View>("EmailConfirmationView");

            MsfTimer.WaitForEndOfFrame(() =>
            {
                if (useDefaultCredentials && Application.isEditor)
                {
                    signinView.SetInputFieldsValues(defaultUsername, defaultPassword);
                    signupView.SetInputFieldsValues(defaultUsername, defaultEmail, defaultPassword);
                }

                if (IsConnected)
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);
                    signinView.Show();
                }
                else
                {
                    Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Connecting to master server... Please wait!");
                }
            });
        }

        public void SignIn()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Signing in... Please wait!");

            logger.Debug("Signing in... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.SignIn(signinView.Username, signinView.Password, (accountInfo, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        signinView.Hide();

                        if (accountInfo.IsEmailConfirmed)
                        {
                            OnSignedInEvent?.Invoke();

                            //Use this to create a message when a user logs in
                            //Msf.Events.Invoke(Event_Keys.showOkDialogBox,
                            //new OkDialogBox_ViewEventMessage($"You have successfuly signed in as {Msf.Client.Auth.AccountInfo.Username} and now you can create another part of your cool game!"));

                            logger.Debug($"You are successfully logged in as {Msf.Client.Auth.AccountInfo.Username}");
                        }
                        else
                        {
                            emailConfirmationView.Show();
                        }
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing in: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignUp()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Signing up... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                string username = signupView.Username;
                string email = signupView.Email;
                string password = signupView.Password;

                var credentials = new Dictionary<string, string>
                {
                    { "username", username },
                    { "email", email },
                    { "password", password }
                };

                Msf.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        signupView.Hide();
                        signinView.SetInputFieldsValues(username, password);
                        signinView.Show();

                        logger.Debug($"You have successfuly signed up. Now you may sign in");
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing up: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignInAsGuest()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Signing in... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.SignInAsGuest((accountInfo, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        signinView.Hide();

                        OnSignedInEvent?.Invoke();

                        Msf.Events.Invoke(Event_Keys.showOkDialogBox,
                        new OkDialogBox_ViewEventMessage($"You have successfuly signed in as {Msf.Client.Auth.AccountInfo.Username} and now you can create another part of your cool game!"));

                        logger.Debug($"You are successfully logged in as {Msf.Client.Auth.AccountInfo.Username}");
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing in: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignOut()
        {
            OnSignedOutEvent?.Invoke();

            // Logout after diconnection
            Msf.Client.Auth.SignOut();

            ViewsManager.HideAllViews();

            Initialize();
        }

        public void Quit()
        {
            Msf.Runtime.Quit();
        }

        public void GetPasswordResetCode()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Sending reset password code... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.RequestPasswordReset(passwordResetCodeView.Email, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        passwordResetCodeView.Hide();
                        passwordResetView.Show();

                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage($"We have sent an email with reset code to your address '{passwordResetCodeView.Email}'", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while password reset code: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void ResetPassword()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Changing password... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.ChangePassword(new PasswordChangeData()
                {
                    Email = passwordResetCodeView.Email,
                    Code = passwordResetView.ResetCode,
                    NewPassword = passwordResetView.NewPassword
                },
                (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        passwordResetView.Hide();
                        signinView.Show();

                        OnPasswordChangedEvent?.Invoke();

                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage("You have successfuly changed your password. Now you can sign in.", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while changing password: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void RequestConfirmationCode()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Sending confirmation code... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        emailConfirmationView.Show();
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage($"We have sent an email with confirmation code to your address '{Msf.Client.Auth.AccountInfo.Email}'", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while requesting confirmation code: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void ConfirmAccount()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Confirming your account... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                string confirmationCode = emailConfirmationView.ConfirmationCode;

                Msf.Client.Auth.ConfirmEmail(confirmationCode, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        emailConfirmationView.Hide();
                        OnEmailConfirmedEvent?.Invoke();
                    }
                    else
                    {
                        outputMessage = $"An error occurred while confirming yor account: {error}";
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        protected override void OnConnectedToMaster()
        {
            Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

            if (Msf.Client.Auth.IsSignedIn)
            {
                OnSignedInEvent?.Invoke();
            }
            else
            {
                signinView.Show();
            }
        }

        protected override void OnDisconnectedFromMaster()
        {
            // Logout after diconnection
            Msf.Client.Auth.SignOut();

            Msf.Events.Invoke(Event_Keys.showOkDialogBox,
                new OkDialogBox_ViewEventMessage("The connection to the server has been lost. "
                + "Please try again or contact the developers of the game or your internet provider.",
                () =>
                {
                    ViewsManager.HideAllViews();
                    Initialize();
                    ConnectionToMaster.Instance.StartConnection();
                }));
        }
    }
}