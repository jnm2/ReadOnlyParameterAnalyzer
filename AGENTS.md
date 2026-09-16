# Coding rules

All C# test source, including snippets, templates, and parameterized test data,
must be passed to a helper whose source parameter is annotated with
`[StringSyntax("C#-Test")]` when test markup is allowed, or
`[StringSyntax("C#")]` when test markup is not allowed. Annotate forwarding
helpers and source-bearing test parameters too. Use the annotated `TestSource`
helpers when assigning source directly to test properties or calling Roslyn APIs.