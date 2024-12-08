using System;
using System.EnterpriseServices;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using StackExchange.Redis;
using Scripting;

namespace redis_com_client
{
    [ComVisible(true)]
    [Guid("1F9E7FA2-7A55-4540-965C-2DE3DFEA0B09")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("RedisComClient")]
    [Synchronization(SynchronizationOption.Disabled)]
    public class RedisClient : IRedisClient
    {
        private IDatabase _redisinstance;
        // TODO: configure hostname and port externally in the COM client
        public RedisClient() { }

        public void Open(string configuration)
        {
            _redisinstance = CacheFactory.GetInstance(configuration:configuration);
        }

        public void Close(bool allowCommandsToComplete = true)
        {
            CacheFactory.Close(allowCommandsToComplete);
        }

        public void Del(string key)
        {
            _redisinstance.KeyDelete(key);
        }
        public bool Exists(string key)
        {
            return _redisinstance.KeyExists(key);
        }
        public void ExpireAt(string key, DateTime ExpireDatetime)
        {
            _redisinstance.KeyExpire(key, ExpireDatetime);
        }
        public void Expire(string key, int seconds)
        {
            DateTime ExpireDatetime = DateTime.Now.AddSeconds(seconds);
            ExpireAt(key, ExpireDatetime);
        }
        public int TTL(string key)
        {
            TimeSpan? interval = _redisinstance.KeyTimeToLive(key);
            if (interval.HasValue) return Convert.ToInt32(interval.Value.TotalSeconds);
            return -1;
        }

        public object Get(string key)
        {
            string pair = _redisinstance.StringGet(key);

            if (string.IsNullOrEmpty(pair))
                return null;

            if (!pair.Contains("ArrayCollumn"))
                return pair;

            var table = JsonConvert.DeserializeObject<MyTable>(pair);
            try
            {
                return (object[,])table.GetArray();
            }
            catch (Exception)
            {
                return (object[])table.GetArray();
            }
        }

        public void Set(string key, object value, int SecondsToExpire)
        {
            object valueToAdd = value?.ToString() ?? string.Empty;

            if (value != null && value.GetType().IsArray)
            {
                try
                {
                    var array = (object[,])value;
                    var table = new MyTable(array);
                    valueToAdd = JsonConvert.SerializeObject(table);
                }
                catch (Exception ex)
                {
                    if (ex.Message.IndexOf("cast object", StringComparison.InvariantCultureIgnoreCase) > 0) //most likely the array is not bi-dimensional, try again with only 1 dimenion
                    {
                        var array = (object[])value;
                        var table = new MyTable(array);
                        valueToAdd = JsonConvert.SerializeObject(table);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (SecondsToExpire > 0)
            {
                _redisinstance.StringSet(key, (string)valueToAdd, TimeSpan.FromSeconds(SecondsToExpire));
            }
            else
            {
                _redisinstance.StringSet(key, (string)valueToAdd);
            }
        }

        public void SetPermanent(string key, object value)
        {
            Set(key, value, 0);
        }

        public void Persist(string key)
        {
            _redisinstance.KeyPersist(key);
        }

        public string Type(string key)
        {
            return _redisinstance.KeyType(key).ToString();
        }

        public double Incr(string key)
        {
            return _redisinstance.StringIncrement(key);
        }
        public double IncrBy(string key, double increment)
        {
            return _redisinstance.StringIncrement(key, increment);
        }
        public double Decr(string key)
        {
            return _redisinstance.StringDecrement(key);
        }
        public double DecrBy(string key, double decrement)
        {
            return _redisinstance.StringDecrement(key, decrement);
        }

        public void Hdel(string key, string field)
        {
            _redisinstance.HashDelete(key, field);
        }
        public bool Hexists(string key, string field)
        {
            return _redisinstance.HashExists(key, field);
        }
        public object Hget(string key, string field)
        {
            // although redis accepts different types of keys, 
            // in the COM interop we only use a string type
            return (string)_redisinstance.HashGet(key, field);
        }

        public object Hgetall(string hashKey)
        {
            var entries = _redisinstance.HashGetAll(hashKey);
            var result = new Dictionary();
            foreach (var entry in entries)
            {
                result.Add(entry.Name.ToString(), entry.Value.ToString());
            }
            return result;
        }

        public long Hlen(string key)
        {
            return _redisinstance.HashLength(key);
        }

        public bool Hset(string key, string field, string value)
        {
            return _redisinstance.HashSet(key, field, value);
        }

        public void HsetDict(string key, object data)
        {
            Scripting.Dictionary dic = (Scripting.Dictionary) data;

            if (dic == null)
            {
                throw new ArgumentException("Data must be of type Scripting.Dictionary.");
            }
            int index = 0;

            HashEntry[] hashEntries = new HashEntry[dic.Count];

            foreach (string keyStr in dic)
            {
                object value = dic.get_Item(keyStr);
                hashEntries[index] = new HashEntry(keyStr, value.ToString());
                index++;
            }

            _redisinstance.HashSet(key, hashEntries); 
        }

        public void SetExpiration(string key, int milliseconds)
        {
            _redisinstance.KeyExpire(key, TimeSpan.FromMilliseconds(milliseconds));
        }

        public void RemoveKeysWithPrefix(string prefix)
        {
            var mask = $"{prefix}*";
            _redisinstance.ScriptEvaluate("local keys = redis.call('keys', ARGV[1]) for i=1,#keys,5000 do redis.call('del', unpack(keys, i, math.min(i+4999, #keys))) end return keys", null, new RedisValue[] { mask });
        }




        public object this[string key]
        {
            get { return Get(key); }
            set { SetPermanent(key, value); }
        }



    }
}
