using System;
using Newtonsoft.Json;

namespace IoTPowerShellAgent.Utilities
{



    public readonly struct ConvertToJsonContext
    {
        public ConvertToJsonContext(int maxDepth, bool enumsAsStrings, bool compressOutput)
        {
            this = new ConvertToJsonContext(maxDepth, enumsAsStrings, compressOutput, StringEscapeHandling.Default);
        }

        public ConvertToJsonContext(int maxDepth, bool enumsAsStrings, bool compressOutput,
            StringEscapeHandling stringEscapeHandling)
        {
            this.MaxDepth = maxDepth;
            this.StringEscapeHandling = stringEscapeHandling;
            this.EnumsAsStrings = enumsAsStrings;
            this.CompressOutput = compressOutput;
        }

        public readonly int MaxDepth;

        public readonly StringEscapeHandling StringEscapeHandling;

        public readonly bool EnumsAsStrings;

        public readonly bool CompressOutput;
    }
}
