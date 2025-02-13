using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Debug
{
    public record LogProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime TimeStamp { get; set; }

        public LogProperty(string key, string value)
        {
            Key = key;
            Value = value;
            TimeStamp = DateTime.UtcNow;

        }
    }
}
