using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace DanielsToolbox.Extensions
{
    public static class CommandExtensions
    {
        public static void Add(this Command command, IEnumerable<Symbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                if (symbol is Argument arg)
                {
                    command.AddArgument(arg);
                }
                else if (symbol is Option opt)
                {
                    if (command.Contains(symbol))
                        continue;

                    command.AddOption(opt);
                }
            }
        }
    }
}
