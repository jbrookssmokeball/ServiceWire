﻿using ServiceWire;
using ServiceWire.Messaging;
using ServiceWire.ZeroKnowledge;
using Xunit;

namespace ServiceWireTests
{
    public class ZkProtocolTests
    {
        [Fact]
        public void BigIntegerArrayTest()
        {
            var bigint = new ZkBigInteger(ZkSafePrimes.N4);
            var bytes = ZkBigInteger.ToByteArray(bigint);
            Assert.Equal(ZkSafePrimes.N4.Length, bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.Equal(ZkSafePrimes.N4[i], bytes[i]);
            }
            var big2 = new ZkBigInteger(bytes);
            Assert.Equal(bigint, big2);
        }

        [Fact]
        public void SimpleProtocolTest()
        {
            var sr = new ZkProtocol();
            var username = "myuser@userdomain.com";
            var pwd = "cc3a6a12-0e5b-47fb-ae45-3485e34582d4";

            // prerequisit: generate password hash that would be stored on server
            var pwdHash = sr.HashCredentials(username, pwd);

            // Step 1. Client sends username and ephemeral hash of random number.
            var aRand = sr.CryptRand();
            var aClientEphemeral = sr.GetClientEphemeralA(aRand);
            // send username and aClientEphemeral to server

            // Step 2. Server looks up username, gets pwd hash, and sends client ephemeral has of params.
            var bRand = sr.CryptRand();
            var bServerEphemeral = sr.GetServerEphemeralB(pwdHash.Salt, pwdHash.Verifier, bRand);
            // send salt and bServerEphemeral to client
            var clientSalt = pwdHash.Salt;

            // Step 3. Client and server calculate random scramble of ephemeral hash values exchanged.
            var clientScramble = sr.CalculateRandomScramble(aClientEphemeral, bServerEphemeral);
            var serverScramble = sr.CalculateRandomScramble(aClientEphemeral, bServerEphemeral);

            var scrambleSame = clientScramble.IsEqualTo(serverScramble);

            // Step 4. Client computes session key
            var clientSessionKey = sr.ClientComputeSessionKey(clientSalt, username, pwd, aClientEphemeral, bServerEphemeral, clientScramble);

            // Step 5. Server computes session key
            var serverSessionKey = sr.ServerComputeSessionKey(pwdHash.Salt, pwdHash.Key, aClientEphemeral, bServerEphemeral, serverScramble);

            var sessionKeysSame = clientSessionKey.IsEqualTo(serverSessionKey);


            // Step 6. Client creates hash of session key and sends to server. Server creates same key and verifies.
            var clientSessionHash = sr.ClientCreateSessionHash(username, pwdHash.Salt, aClientEphemeral,
                bServerEphemeral, clientSessionKey);
            // send to server and server verifies
            // server validates clientSessionHash is same as serverClientSessionHash 
            var serverClientSessionHash = sr.ClientCreateSessionHash(username, pwdHash.Salt, aClientEphemeral,
                bServerEphemeral, serverSessionKey);

            var clientEqualToServer = clientSessionHash.IsEqualTo(serverClientSessionHash);

            // Step 7. Server creates hash of session key and sends to client. Client creates same key and verifies.
            var serverSessionHash = sr.ServerCreateSessionHash(aClientEphemeral, clientSessionHash, serverSessionKey);
            // server sends serverSessionHash to client
            // validate that serverSessionHash is same as clientServerSessionHash
            var clientServerSessionHash = sr.ServerCreateSessionHash(aClientEphemeral, clientSessionHash, clientSessionKey);

            var serverEqualToClient = serverSessionHash.IsEqualTo(clientServerSessionHash);

            //proof
            Assert.True(sessionKeysSame);
            Assert.True(scrambleSame);
            Assert.True(clientEqualToServer);
            Assert.True(serverEqualToClient);

            var data = sr.Combine(sr.CryptRand(), sr.CryptRand(), sr.CryptRand());
            var crypto = new ZkCrypto(clientSessionKey, clientScramble, new NullLogger());
            var encrypted = crypto.Encrypt(data);
            var decrypted = crypto.Decrypt(encrypted);
            var cryptSame = data.IsEqualTo(decrypted);
            Assert.True(cryptSame);
        }
    }
}
