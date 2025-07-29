using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ChatBox.API.Plugins
{
    public class SummaryPlugin
    {
        [KernelFunction]
        [Description("Tóm tắt văn bản dài thành đoạn ngắn gọn")]
        public async Task<string> SummarizeText(
            KernelArguments arguments,
            [Description("Văn bản cần tóm tắt")] string text,
            [Description("Độ dài tóm tắt (ngắn/trung bình/dài)")] string length = "trung bình")
        {
            var kernel = arguments["kernel"] as Kernel;
            if (kernel == null) return "Lỗi: Không thể truy cập kernel";

            var maxWords = length.ToLower() switch
            {
                "ngắn" => "50 từ",
                "dài" => "200 từ",
                _ => "100 từ"
            };

            var prompt = $@" Hãy tóm tắt văn bản sau thành khoảng {maxWords} bằng tiếng Việt: {text} Tóm tắt:";

            var summaryFunction = kernel.CreateFunctionFromPrompt(prompt);
            var result = await kernel.InvokeAsync(summaryFunction);

            return result.ToString();
        }
    }
}
