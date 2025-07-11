using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTOs
{
    public class OllamaConfigSetting
    {
        public string Endpoint { get; set; }
        public string TextModel { get; set; }
        public string EmbeddingModel { get; set; }
        public float Temperature { get; set; }
        public float TopP { get; set; }
        public int TopK { get; set; }
        public int NumPredict { get; set; }

    }

}
