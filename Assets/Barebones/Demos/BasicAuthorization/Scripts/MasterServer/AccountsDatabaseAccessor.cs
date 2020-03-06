using Aevien.Utilities;
using Barebones.MasterServer;
using LiteDB;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.MasterServer.Examples.BasicAuthorization
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private readonly LiteCollection<AccountInfoLiteDb> accounts;
        private readonly LiteCollection<PasswordResetData> resetCodes;
        private readonly LiteCollection<EmailConfirmationData> emailConfirmationCodes;

        private readonly LiteDatabase database;

        public AccountsDatabaseAccessor(LiteDatabase database)
        {
            this.database = database;

            accounts = this.database.GetCollection<AccountInfoLiteDb>("accounts");
            accounts.EnsureIndex(a => a.Username, true);
            accounts.EnsureIndex(a => a.Email, true);

            resetCodes = this.database.GetCollection<PasswordResetData>("resetCodes");
            resetCodes.EnsureIndex(a => a.Email, true);

            emailConfirmationCodes = this.database.GetCollection<EmailConfirmationData>("emailConfirmationCodes");
            emailConfirmationCodes.EnsureIndex(a => a.Email, true);
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoLiteDb();
        }

        public IAccountInfoData GetAccountByUsername(string username)
        {
            return accounts.FindOne(a => a.Username == username);
        }

        public IAccountInfoData GetAccountByToken(string token)
        {
            return accounts.FindOne(a => a.Token == token);
        }

        public IAccountInfoData GetAccountByEmail(string email)
        {
            var emailLower = email.ToLower();
            return accounts.FindOne(Query.EQ("Email", emailLower));
        }

        public void SavePasswordResetCode(IAccountInfoData account, string code)
        {
            resetCodes.Delete(Query.EQ("Email", account.Email.ToLower()));

            resetCodes.Insert(new PasswordResetData()
            {
                Id = ObjectId.NewObjectId(),
                Email = account.Email,
                Code = code
            });
        }

        public IPasswordResetData GetPasswordResetData(string email)
        {
            return resetCodes.FindOne(Query.EQ("Email", email.ToLower()));
        }

        public void SaveEmailConfirmationCode(string email, string code)
        {
            emailConfirmationCodes.Delete(Query.EQ("Email", email.ToLower()));

            emailConfirmationCodes.Insert(new EmailConfirmationData()
            {
                Id = ObjectId.NewObjectId(),
                Code = code,
                Email = email
            });
        }

        public string GetEmailConfirmationCode(string email)
        {
            var entry = emailConfirmationCodes.FindOne(Query.EQ("Email", email));
            return entry != null ? entry.Code : null;
        }

        public void UpdateAccount(IAccountInfoData account)
        {
            accounts.Update(account as AccountInfoLiteDb);
        }

        public void InsertNewAccount(IAccountInfoData account)
        {
            accounts.Insert(account as AccountInfoLiteDb);
        }

        public void InsertToken(IAccountInfoData account, string token)
        {
            account.Token = token;
            accounts.Update(account as AccountInfoLiteDb);
        }

        private class PasswordResetData : IPasswordResetData
        {
            [BsonId]
            public ObjectId Id { get; set; }
            public string Email { get; set; }
            public string Code { get; set; }
        }

        private class EmailConfirmationData
        {
            [BsonId]
            public ObjectId Id { get; set; }
            public string Email { get; set; }
            public string Code { get; set; }
        }

        /// <summary>
        /// LiteDB implementation of account data
        /// </summary>
        private class AccountInfoLiteDb : IAccountInfoData
        {
            [BsonId]
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsGuest { get; set; }
            public bool IsEmailConfirmed { get; set; }
            public Dictionary<string, string> Properties { get; set; }

            public event Action<IAccountInfoData> OnChangedEvent;

            public AccountInfoLiteDb()
            {
                Username = string.Empty;
                Password = string.Empty;
                Email = string.Empty;
                Token = string.Empty;
                IsAdmin = false;
                IsGuest = false;
                IsEmailConfirmed = false;
                Properties = new Dictionary<string, string>();
            }

            public void MarkAsDirty()
            {
                OnChangedEvent?.Invoke(this);
            }
        }
    }
}
