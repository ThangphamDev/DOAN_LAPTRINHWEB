using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DOAN_LAPTRINHWEB.Models
{
    public class VnPayLibrary
    {
        private readonly IConfiguration _configuration;
        private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

        public VnPayLibrary(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class VnPayCompare : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                var vnpCompare = CompareInfo.GetCompareInfo("en-US");
                return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
            }
        }

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _requestData.Add(key, value);
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _responseData.Add(key, value);
            }
        }

        // Sửa phương thức CreateRequestUrl để gỡ lỗi
        public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
        {
            try
            {
                StringBuilder data = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in _requestData)
                {
                    if (!String.IsNullOrEmpty(kv.Value))
                    {
                        data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    }
                }

                string queryString = data.ToString();
                string rawData = queryString;
                if (!string.IsNullOrEmpty(rawData) && rawData.EndsWith("&"))
                {
                    rawData = rawData.Remove(rawData.Length - 1);
                }

                // Log để kiểm tra
                Console.WriteLine("Raw data for hash: " + rawData);

                // Tạo HMAC-SHA512 hash
                string vnpSecureHash = HmacSHA512(vnpHashSecret, rawData);

                // Log hash được tạo
                Console.WriteLine("Generated hash: " + vnpSecureHash);

                // Tạo URL hoàn chỉnh
                string paymentUrl = baseUrl + "?" + queryString + "vnp_SecureHash=" + vnpSecureHash;

                return paymentUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in CreateRequestUrl: " + ex.Message);
                throw;
            }
        }

        public bool ValidateSignature(string inputHash, string secretKey)
        {
            string rspRaw = GetResponseData();
            string myChecksum = HmacSHA512(secretKey, rspRaw);
            return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string HmacSHA512(string key, string inputData)
        {
            try
            {
                var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
                var hashData = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
                var sbHash = new StringBuilder();

                foreach (var b in hashData)
                {
                    // Chuyển đổi sang chuỗi hex, định dạng với 2 chữ số, chữ thường
                    sbHash.Append(b.ToString("x2"));
                }

                return sbHash.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in HmacSHA512: " + ex.Message);
                throw;
            }
        }

        private string GetResponseData()
        {
            StringBuilder data = new StringBuilder();
            if (_responseData.ContainsKey("vnp_SecureHashType"))
            {
                _responseData.Remove("vnp_SecureHashType");
            }
            if (_responseData.ContainsKey("vnp_SecureHash"))
            {
                _responseData.Remove("vnp_SecureHash");
            }
            foreach (KeyValuePair<string, string> kv in _responseData)
            {
                if (!String.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            return data.ToString().Remove(data.Length - 1, 1);
        }
    }
}