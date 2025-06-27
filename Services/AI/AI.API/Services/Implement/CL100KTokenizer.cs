using Microsoft.KernelMemory.AI; 
using Tiktoken; 
using Tiktoken.Encodings; 


namespace AI.API.Services.Implement
{
    public class CL100KTokenizer : ITextTokenizer
    {
        private readonly Encoder _encoder;
        public CL100KTokenizer()
        {
            _encoder = new Encoder(new Cl100KBase()); 
        }

        public int CountTokens(string text)
        {
            return _encoder.CountTokens(text);
        }

        public IReadOnlyList<string> GetTokens(string text)
        {

            return _encoder.Explore(text).ToList(); 
            // return _encoder.Encode(text).Select(id => _encoder.Decode(new[] { id })).ToList();
        }
    }

}