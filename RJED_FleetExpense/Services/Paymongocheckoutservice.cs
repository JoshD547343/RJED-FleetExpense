using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RJED_FleetExpense.Services
{
    public class PayMongoOptions
    {
        public string SecretKey { get; set; } = "";  
    }

    public record CheckoutCreated(bool Success, string? SessionId, string? CheckoutUrl, string? Error);
    public record CheckoutStatus(bool Success, bool Paid, string? PaymentId, string? Error);

    public interface IPayMongoCheckoutService
    {
        Task<CheckoutCreated> CreateAsync(decimal amount, string name, string description,
                                          string referenceNumber, string successUrl, string cancelUrl,
                                          string[] methods,
                                          string? billingName = null, string? billingEmail = null);
        Task<CheckoutStatus> GetStatusAsync(string sessionId);
    }

    public class PayMongoCheckoutService : IPayMongoCheckoutService
    {
        private readonly HttpClient _http;

        public PayMongoCheckoutService(HttpClient http, IOptions<PayMongoOptions> opt)
        {
            _http = http;
            _http.BaseAddress = new Uri("https://api.paymongo.com/v1/");

            var key = (opt.Value.SecretKey ?? "").Trim();
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("RJED-FleetExpense/1.0");
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<CheckoutCreated> CreateAsync(decimal amount, string name, string description,
            string referenceNumber, string successUrl, string cancelUrl, string[] methods,
            string? billingName = null, string? billingEmail = null)
        {
            var attributes = new Dictionary<string, object?>
            {
                ["line_items"] = new[]
                {
                    new
                    {
                        currency = "PHP",
                        amount = (long)Math.Round(amount * 100m),   // centavos
                        name,
                        quantity = 1
                    }
                },
                ["payment_method_types"] = methods,
                ["description"] = description,
                ["reference_number"] = referenceNumber,
                ["success_url"] = successUrl,
                ["cancel_url"] = cancelUrl
            };

            // Prefill the customer form so the accountant does not have to type anything
            if (!string.IsNullOrWhiteSpace(billingName) || !string.IsNullOrWhiteSpace(billingEmail))
            {
                attributes["billing"] = new Dictionary<string, string?>
                {
                    ["name"] = billingName,
                    ["email"] = billingEmail
                };
            }

            var payload = JsonSerializer.Serialize(new { data = new { attributes } });
            var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var res = await _http.PostAsync("checkout_sessions", content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new CheckoutCreated(false, null, null, json + $" [HTTP {(int)res.StatusCode}]");

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            return new CheckoutCreated(true,
                data.GetProperty("id").GetString(),
                data.GetProperty("attributes").GetProperty("checkout_url").GetString(),
                null);
        }

        public async Task<CheckoutStatus> GetStatusAsync(string sessionId)
        {
            var res = await _http.GetAsync($"checkout_sessions/{sessionId}");
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return new CheckoutStatus(false, false, null, json + $" [HTTP {(int)res.StatusCode}]");

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var attrs = data.GetProperty("attributes");

            // 1) Paid sessions list their payments; look for one with status "paid".
            if (attrs.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in payments.EnumerateArray())
                {
                    if (p.TryGetProperty("attributes", out var pAttrs) &&
                        pAttrs.TryGetProperty("status", out var st) && st.GetString() == "paid")
                    {
                        var pid = p.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        return new CheckoutStatus(true, true, pid, null);
                    }
                }
            }

            // 2) Fallback: some responses carry a status on the session itself.
            if (attrs.TryGetProperty("status", out var sessionStatus) &&
                sessionStatus.ValueKind == JsonValueKind.String)
            {
                var s = sessionStatus.GetString();
                if (s == "paid" || s == "succeeded" || s == "completed")
                    return new CheckoutStatus(true, true, data.GetProperty("id").GetString(), null);
            }

            return new CheckoutStatus(true, false, null, null);
        }
    }
}