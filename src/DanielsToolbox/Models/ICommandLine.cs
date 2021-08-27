using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanielsToolbox.Models
{
    public interface ICommandLine
    {
        Command Create();

        IEnumerable<Symbol> Arguments();
    }
}
