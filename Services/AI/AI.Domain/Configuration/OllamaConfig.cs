using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Domain.Configuration
{
    public class OllamaConfig
    {
        public string Host { get; set; } = "http://localhost:11434";
        public string ModelName { get; set; } = "deepseek-r1:1.5b";   
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public int NumPredict { get; set; } = 1024;
    }
}
