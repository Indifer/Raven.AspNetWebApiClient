﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Raven.AspNetWebApiClient.IContractModel
{
    /// <summary>
    /// 键值对结构
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class KeyValue<TKey, TValue>
    {
        /// <summary>
        /// 获取键/值对中的键
        /// </summary>
        [DataMember(Name = "K")]
        public TKey Key { get; set; }

        /// <summary>
        /// 获取键/值对中的值
        /// </summary>
        [DataMember(Name = "V")]
        public TValue Value { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public KeyValue(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

    }
}
