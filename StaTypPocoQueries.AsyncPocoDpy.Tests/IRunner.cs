//Copyright © 2018 Dominik Pytlewski. Licensed under Apache License 2.0. See LICENSE file for details

using System;
using System.Threading.Tasks;
using AsyncPoco;

namespace StaTypPocoQueries.AsyncPocoDpy.Tests {
    public interface IRunner {
        Task Run(Action<string> logger, Func<Database,Task> testBody);
    }
}
