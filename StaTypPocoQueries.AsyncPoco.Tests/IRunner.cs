using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPoco.Tests {
    public interface IRunner {
        Task Run(Action<string> logger, Func<Database,Task> testBody);
    }
}
