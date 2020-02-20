﻿using SQRLDotNetClientUI.DB.DBContext;
using SQRLDotNetClientUI.DB.Models;
using SQRLUtilsLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace SQRLDotNetClientUI.Models
{
    /// <summary>
    /// Provides functionality for storing, retrieving and
    /// managing SQRL identities.
    /// </summary>
    public sealed class IdentityManager
    {
        private static readonly Lazy<IdentityManager> _instance = new Lazy<IdentityManager>(() => new IdentityManager());
        private SQRLDBContext _db;
        private Identity _currentIdentity;

        /// <summary>
        /// The constructor is private because <c>IdentityManager</c> 
        /// implements the singleton pattern. To get an instance, use 
        /// <c>IdentityManager.Instance</c> instead.
        /// </summary>
        private IdentityManager()
        {
            _db = new SQRLDBContext();
            _currentIdentity = GetIdentityInternal(GetUserData().LastLoadedIdentity);
        }

        /// <summary>
        /// Returns the singleton <c>IdentityManager</c> instance. If 
        /// the instance does not exists yet, it will first be created.
        /// </summary>
        public static IdentityManager Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        /// <summary>
        /// Returns the currently active identity.
        /// </summary>
        public SQRLIdentity CurrentIdentity
        {
            get
            {
                if (_currentIdentity == null) return null;
                return DeserializeIdentity(_currentIdentity.DataBytes);
            }
        }

        /// <summary>
        /// Tries to set the identity with the given <paramref name="uniqueId"/> as
        /// the currently active identity.
        /// </summary>
        /// <param name="uniqueId">The unique id of the identity to be set active.</param>
        public void ChangeCurrentIdentity(string uniqueId)
        {
            if (_currentIdentity?.UniqueId == uniqueId) return;

            Identity id = GetIdentityInternal(uniqueId);
            if (id == null) throw new ArgumentException("No matching identity found!", nameof(uniqueId));

            _currentIdentity = id;
            GetUserData().LastLoadedIdentity = id.UniqueId;
            _db.SaveChanges();

            IdentityChanged?.Invoke(this, new IdentityChangedEventArgs(id.Name, id.UniqueId));
        }

        /// <summary>
        /// Imports a SQRL identity and stores it in the database.
        /// </summary>
        /// <param name="identity">The <c>SQRLIdentity</c> to be imported.</param>
        /// <param name="setAsCurrentIdentity">If set to <c>true</c>, the imported identity will be 
        /// set as the currently active identity after adding it to the database.</param>
        public void ImportIdentity(SQRLIdentity identity, bool setAsCurrentIdentity = true)
        {
            Identity newIdRec = new Identity();
            newIdRec.Name = identity.IdentityName;
            newIdRec.UniqueId = identity.Block0.UniqueIdentifier.ToHex();
            newIdRec.GenesisId = identity.Block0.GenesisIdentifier.ToHex();

            // Serialize the identity for storing it in the database.
            // We could use identity.ToByteArray() here, but then we would
            // lose extra information not covered by the S4 format, such
            // as identity name, file path etc.
            newIdRec.DataBytes = SerializeIdentity(identity);

            _db.Identities.Add(newIdRec);
            _db.SaveChanges();

            if (setAsCurrentIdentity)
            {
                ChangeCurrentIdentity(newIdRec.UniqueId);
            }
        }

        /// <summary>
        /// Retrieves the <c>SQRLIdentity</c> with the given <paramref name="uniqueId"/>
        /// from the database. If no such identity was found, the method returns <c>null</c>.
        /// </summary>
        /// <param name="uniqueId">The unique id of the identity to be returned.</param>
        public SQRLIdentity GetIdentity(string uniqueId)
        {
            Identity id = GetIdentityInternal(uniqueId);
            if (id == null) return null;

            return DeserializeIdentity(id.DataBytes);
        }

        /// <summary>
        /// Returns a list of available identities (name and unique id).
        /// </summary>
        /// <returns>A list of <c>Tuple</c>s, each containing two strings, where the first 
        /// one is the identity's name and the second is the identity's unique id.</returns>
        public List<Tuple<string, string>> GetIdentities()
        {
            List<Tuple<string, string>> result = new List<Tuple<string, string>>();

            foreach (var id in _db.Identities)
                result.Add(new Tuple<string, string>(id.Name, id.UniqueId));

            return result;
        }

        /// <summary>
        /// Retrieves the <c>Identity</c> with the given <paramref name="uniqueId"/>
        /// from the database. If no such identity was found, the method returns <c>null</c>.
        /// </summary>
        /// <param name="uniqueId">The unique id of the identity to be returned.</param>
        private Identity GetIdentityInternal(string uniqueId)
        {
            if (_db.Identities.Count() < 1) return null;

            return _db.Identities
                .Single(i => i.UniqueId == uniqueId);
        }

        /// <summary>
        /// Returns the <c>UserData</c> database entry. If no such entry
        /// exists, it will be created.
        /// </summary>
        private UserData GetUserData()
        {
            UserData result = null;

            result = _db.UserData.FirstOrDefault();
            if (result == null)
            {
                UserData ud = new UserData();
                ud.LastLoadedIdentity = string.Empty;
                _db.UserData.Add(ud);
                _db.SaveChanges();
                result = ud;
            }

            return result;
        }

        /// <summary>
        /// Serializes a <c>SQRLIdentity</c> object to a byte array.
        /// </summary>
        /// <param name="identity">The identity to be serialized.</param>
        private byte[] SerializeIdentity(SQRLIdentity identity)
        {
            byte[] identityBtes;
            IFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, identity);
                identityBtes = stream.ToArray();
            }
            return identityBtes;
        }

        /// <summary>
        /// Deserializes a <c>SQRLIdentity</c> object from a byte array.
        /// </summary>
        /// <param name="dataBytes">The byte array representing the serialized <c>SQRLIdentity</c>.</param>
        private SQRLIdentity DeserializeIdentity(byte[] dataBytes)
        {
            IFormatter formatter = new BinaryFormatter();
            return (SQRLIdentity)formatter.Deserialize(new MemoryStream(dataBytes));
        }

        /// <summary>
        /// This event is raised if the currently selected identity changes.
        /// </summary>
        public event EventHandler<IdentityChangedEventArgs> IdentityChanged;
    }

    public class IdentityChangedEventArgs : EventArgs
    {
        public string IdentityName { get; }
        public string IdentityUniqueId { get; }

        public IdentityChangedEventArgs(string identityName, string identityUniqueId)
        {
            IdentityName = identityName;
            IdentityUniqueId = identityUniqueId;
        }
    }
}