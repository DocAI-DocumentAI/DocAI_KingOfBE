namespace AI.API.Extensions
{
    public class LoggingHandler : DelegatingHandler
    {
        public LoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.WriteLine("➡️ HTTP Request:");
            Console.WriteLine($"{request.Method} {request.RequestUri}");

            if (request.Content != null)
            {
                var requestContent = await request.Content.ReadAsStringAsync();
                Console.WriteLine("Body:");
                Console.WriteLine(requestContent);
            }

            var response = await base.SendAsync(request, cancellationToken);

            Console.WriteLine("⬅️ HTTP Response:");
            Console.WriteLine($"Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseContent);

            return response;
        }
    }

}