using Tscs.Conformance;
try { FixtureProtocol.Run(Console.In,Console.Out); return 0; }
catch(Exception e) { Console.Error.WriteLine(e); return 1; }
