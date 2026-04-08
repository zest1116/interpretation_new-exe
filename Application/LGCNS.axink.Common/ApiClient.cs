using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common
{
    public static class ApiClient
    {
        private static readonly HttpClient _http = new();

        public static async Task<T?> GetAsync<T>(string url)
        => await _http.GetFromJsonAsync<T>(url);
    }
}
