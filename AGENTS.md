# Coding rules

All C# test source, including snippets, templates, and parameterized test data,
must be passed to a helper whose source parameter is annotated with
`[StringSyntax("C#-Test")]` when test markup is allowed, or
`[StringSyntax("C#")]` when test markup is not allowed. Annotate forwarding
helpers and source-bearing test parameters too. Use the annotated `TestSource`
helpers when assigning source directly to test properties or calling Roslyn APIs.

Prefer individually named `[Test]` methods over parameterized tests when the
test data contains multiline C# source. Give each case a descriptive name and
keep source in the test body, passed through the annotated helpers. Small
single-line or operator parameterization is fine; split every case in a mixed
group if any case contains multiline source.
