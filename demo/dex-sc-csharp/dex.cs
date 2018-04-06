
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace ClearingContract
{
    public class ClearContract : SmartContract
    {
        public static readonly byte[] AdminKey = { 97, 100, 109, 105, 110 };//admin

        //fund-related 
        public static readonly byte[] BalanceKey = { 0x01 };
        public static readonly byte[] AvailBalanceKey = { 0x02 };

        //
        public static readonly byte[] LockOrderStatusKey = { 0x03 };
        public static readonly byte[] LockOrderAmountKey = { 0x04 };
        public static readonly byte[] LockOrderReceiverKey = { 0x05 };
        public static readonly byte[] LockOrderOwnerKey = { 0x06 };
        public static readonly byte[] LockOrderAssetKey = { 0x07 };
        //public static readonly byte[] 
        //public static readonly byte[] 

        //Lock Order status 
        public static readonly byte[] Locked = { 0x01 };
        public static readonly byte[] Unlocked = { 0x02 };

        [DisplayName("refund")]
        public static event Action<byte[], byte[], BigInteger> Refund;

        public static Object Main(string op, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                //clear money               
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (op == "deposit")
                {
                    byte[] ontid    = (byte[])args[0];
                    byte[] assetid  = (byte[])args[1];
                    int    amount   = (int)   args[2];

                    return Deposit(ontid, assetid, amount);
                }
                else if (op == "lock")
                {
                    byte[] serial_no     = (byte[])args[0];
                    byte[] user_ontid    = (byte[])args[1];
                    byte[] buyer_ontid   = (byte[])args[2];
                    byte[] asset_id      = (byte[])args[3];
                    byte[] amount        = (byte[])args[4];
                    byte[] sig           = (byte[])args[5];

                    return Lock(serial_no, user_ontid, buyer_ontid, asset_id, amount, sig);
                }
                else if (op == "clear")
                {
                    byte[] serial_no         = (byte[])args[0];
                    byte[] user_ontid        = (byte[])args[1];
                    byte[] asset_id          = (byte[])args[2];
                    byte[] issuer_id         = (byte[])args[3];
                    BigInteger user_bounty   = ((byte[])args[4]).AsBigInteger();
                    BigInteger issuer_bounty = ((byte[])args[5]).AsBigInteger();
                    byte[] user_sig          = (byte[])args[6];

                    return Clear(serial_no, user_ontid, asset_id, issuer_id, user_bounty, issuer_bounty, user_sig);
                }
            }
            return false;
        }

        public static bool Clear(byte[] serial_no, byte[] user_ontid, byte[] asset_id, byte[] issuer_id,
                            BigInteger user_bounty, BigInteger issuer_bounty, byte[] user_sig)
        {
            byte[] orderStatus = Storage.Get(Storage.CurrentContext, LockOrderStatusKey.Concat(serial_no));
            if (orderStatus.Length == 0) return false;

            if (orderStatus[0] != Locked[0]) return false;

            byte[] receiver = Storage.Get(Storage.CurrentContext, LockOrderReceiverKey.Concat(serial_no));
            byte[] buyer_ontid = Storage.Get(Storage.CurrentContext, LockOrderOwnerKey.Concat(serial_no));

            if (!Equals(receiver, user_ontid)) return false;

            //TODO: check signature here
            BigInteger amount = Storage.Get(Storage.CurrentContext, LockOrderAmountKey.Concat(serial_no)).AsBigInteger();
            if (user_bounty + issuer_bounty > amount) return false;

            //user += user_bounty
            //issuer += issuer_bounty
            AlterBalance(user_ontid, asset_id, user_bounty, "add", "both");
            AlterBalance(issuer_id, asset_id, issuer_bounty, "add", "both");

            AlterBalance(buyer_ontid, asset_id, amount, "sub", "balance");
            Storage.Put(Storage.CurrentContext, LockOrderStatusKey.Concat(serial_no), Unlocked);
            return true;
        }

        public static bool Lock(byte[] serial_no, byte[] user_ontid, byte[] buyer_ontid, 
                                byte[] asset_id, byte[] amount, byte[] sig)
        {
            /* 
             * status: 
             *  - does not exist
             *  - locking
             *  - unlocked(give back or clear to other accounts)
             */
            byte[] orderStatus = Storage.Get(Storage.CurrentContext, LockOrderStatusKey.Concat(serial_no));
            if (orderStatus.Length != 0) return false;

            //the order does not exist
            //TODO: check buyer's signature

            BigInteger val = amount.AsBigInteger();
            if ( !AlterBalance(buyer_ontid, asset_id, val, "sub", "avail") ) return false;

            Storage.Put(Storage.CurrentContext, LockOrderStatusKey.Concat(serial_no), Locked);
            Storage.Put(Storage.CurrentContext, LockOrderAmountKey.Concat(serial_no), amount);
            Storage.Put(Storage.CurrentContext, LockOrderAssetKey.Concat(serial_no), asset_id);
            Storage.Put(Storage.CurrentContext, LockOrderOwnerKey.Concat(serial_no), buyer_ontid);
            Storage.Put(Storage.CurrentContext, LockOrderReceiverKey.Concat(serial_no), user_ontid);

            return true;
        }
        
        public static bool Deposit(byte[] ontid, byte[] asset_id, int amount)
        {
            ulong value = GetContributeValue(asset_id);

            AlterBalance(ontid, asset_id, (BigInteger)value, "add", "both");
            return true;
        }

        private static ulong GetContributeValue(byte[] asset_id)
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Asset
            // 获取转入智能合约地址的资产总量
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }

        // get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        private static bool AlterBalance(byte[] ontid, byte[] asset_id, BigInteger amount, string operation, string option)
        {
            BigInteger balance = Storage.Get(Storage.CurrentContext, BalanceKey.Concat(asset_id).Concat(ontid)).AsBigInteger();
            BigInteger availBalance = Storage.Get(Storage.CurrentContext, AvailBalanceKey.Concat(asset_id).Concat(ontid)).AsBigInteger();

            if (operation == "add")
            {
                balance += amount; availBalance += amount;
            }
            else if (operation == "sub")
            {
                if (option == "avail" && availBalance >= amount)
                {
                    availBalance -= amount;
                } 
                else if (option == "balance" && balance >= amount)
                {
                    balance -= amount;
                }
                else if (option == "both" && availBalance >= amount)
                {
                    balance -= amount;
                    availBalance -= amount;
                } 
                else
                {
                    return false;
                }
                
            }
            else
            {
                return false;
            }
            Storage.Put(Storage.CurrentContext, AvailBalanceKey.Concat(asset_id).Concat(ontid), availBalance.AsByteArray());
            Storage.Put(Storage.CurrentContext, BalanceKey.Concat(asset_id).Concat(ontid), balance.AsByteArray());
            return true;
        }

        private static byte[] GetAdmin()
        {
            return Storage.Get(Storage.CurrentContext, AdminKey);
        }

        private static bool Equals(byte[] a, byte[] b)
        {
            if (a.Length == b.Length)
            {
                int i = 0; 
                for (; i < a.Length; i++)
                {
                    if (a[i] != b[i]) {
                        return false;
                    }
                }
                return true;
            } 
            else
            {
                return false;
            }
        }

        private static byte[] GetByte(byte i)
        {
            byte[] bytes = new byte[256] {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
                21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
                31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
                51, 52, 53, 54, 55, 56, 57, 58, 59, 60,
                61, 62, 63, 64, 65, 66, 67, 68, 69, 70,
                71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
                81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
                91, 92, 93, 94, 95, 96, 97, 98, 99, 100,
                101, 102, 103, 104, 105, 106, 107, 108,
                109, 110, 111, 112, 113, 114, 115, 116,
                117, 118, 119, 120, 121, 122, 123, 124,
                125, 126, 127, 128, 129, 130, 131, 132,
                133, 134, 135, 136, 137, 138, 139, 140,
                141, 142, 143, 144, 145, 146, 147, 148, 149, 150,
                151, 152, 153, 154, 155, 156, 157, 158, 159, 160,
                161, 162, 163, 164, 165, 166, 167, 168, 169, 170,
                171, 172, 173, 174, 175, 176, 177, 178, 179, 180,
                181, 182, 183, 184, 185, 186, 187, 188, 189, 190,
                191, 192, 193, 194, 195, 196, 197, 198, 199, 200,
                201, 202, 203, 204, 205, 206, 207, 208, 209, 210,
                211, 212, 213, 214, 215, 216, 217, 218, 219, 220,
                221, 222, 223, 224, 225, 226, 227, 228, 229, 230,
                231, 232, 233, 234, 235, 236, 237, 238, 239, 240,
                241, 242, 243, 244, 245, 246, 247, 248, 249, 250,
                251, 252, 253, 254, 255 };

            return Neo.SmartContract.Framework.Helper.Range(bytes, i, 1);
        }
    }
}
