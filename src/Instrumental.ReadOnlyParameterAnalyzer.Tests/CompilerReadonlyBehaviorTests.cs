namespace Instrumental.ReadOnlyParameterAnalyzer.Tests;

using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

public class CompilerReadonlyBehaviorTests
{
    [Test]
    public async Task Compiler_readonly_accessor_rules()
    {
        var compilation = CSharpCompilation.Create("AccessorProbe",
            [CSharpSyntaxTree.ParseText("""
                using System;
                class C
                {
                    readonly S s;
                    void M() { s.P = 1; s.P++; s.Q = 1; s[0] = 1; s.R++; s.E += M; s.E -= M; }
                }
                struct S
                {
                    public int P { get => 0; set { } }
                    public readonly int Q { get => 0; set { } }
                    public readonly int this[int index] { get => 0; set { } }
                    public int R { get => 0; readonly set { } }
                    public event Action E { add { } remove { } }
                }
                """)], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(2);
        await Assert.That(errors.All(d => d.Id == "CS1648")).IsTrue();
    }

    [Test]
    public async Task Compiler_constructs_copy_only_where_readonly_changes_receiver()
    {
        var compilation = CSharpCompilation.Create("ReadonlyProbe",
            [CSharpSyntaxTree.ParseText("""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using System.Runtime.CompilerServices;
                public class Probe
                {
                    public S Mutable;
                    public readonly S Readonly;
                    public K Countable;
                    public ExplicitEnumerable InterfaceMutable;
                    public readonly ExplicitEnumerable InterfaceReadonly;
                    public S? Nullable = new S();
                    public int Foreach() { foreach (var x in Mutable) { } return Mutable.Count; }
                    public int ReadonlyForeach() { foreach (var x in Readonly) { } return Readonly.Count; }
                    public int InterfaceForeach() { foreach (var x in InterfaceMutable) { } return InterfaceMutable.Count; }
                    public int ReadonlyInterfaceForeach() { foreach (var x in InterfaceReadonly) { } return InterfaceReadonly.Count; }
                    public int NullableForeach() { foreach (var x in Nullable) { } return Nullable.Value.Count; }
                    public int GenericConditional() { var g = new Generic<S>(); g.Run(); return g.Mutable.Count; }
                    public int ReadonlyGenericConditional() { var g = new Generic<S>(); g.Run(); return g.Readonly.Count; }
                    public int UnconstrainedConditional() { var g = new Unconstrained<S>(); g.Run(); return g.Mutable.Count; }
                    public int ReadonlyUnconstrainedConditional() { var g = new Unconstrained<S>(); g.Run(); return g.Readonly.Count; }
                    public async Task<int> AsyncForeach() { await foreach (var x in Mutable) { } return Mutable.Count; }
                    public async Task<int> ReadonlyAsyncForeach() { await foreach (var x in Readonly) { } return Readonly.Count; }
                    public int Using() { using (Mutable) { } return Mutable.Count; }
                    public async Task<int> AsyncUsing() { await using (Mutable) { } return Mutable.Count; }
                    public int Delegate() { Action a = Mutable.Mutate; a(); return Mutable.Count; }
                    public int Interpolation() { var text = $"{Mutable}"; return Mutable.Count; }
                    public int Deconstruct() { var (x, y) = Mutable; return Mutable.Count; }
                    public int ReadonlyDeconstruct() { var (x, y) = Readonly; return Readonly.Count; }
                    public async Task<int> Await() { await Mutable; return Mutable.Count; }
                    public async Task<int> ReadonlyAwait() { await Readonly; return Readonly.Count; }
                    public int Pattern() { if (Mutable is { Property: 1 }) { } return Mutable.Count; }
                    public int ReadonlyPattern() { if (Readonly is { Property: 1 }) { } return Readonly.Count; }
                    public int ReadonlySetterCompound() { Mutable.Mixed++; return Mutable.Count; }
                    public int ReadonlyFieldSetterCompound() { Readonly.Mixed++; return Readonly.Count; }
                    public int Spread() { int[] items = [.. Mutable]; return Mutable.Count; }
                    public int ReadonlySpread() { int[] items = [.. Readonly]; return Readonly.Count; }
                    public int CountableSpread() { int[] items = [.. Countable]; return Countable.Mutations; }
                    public int ThreeTemporarySpread() { int[] items = [0, 0, .. Countable]; return Countable.Mutations; }
                    public int FourTemporarySpread() { int[] items = [0, 0, 0, .. Countable]; return Countable.Mutations; }
                    public unsafe int Fixed() { fixed (int* ptr = Mutable) { } return Mutable.Count; }
                    public unsafe int ReadonlyFixed() { fixed (int* ptr = Readonly) { } return Readonly.Count; }
                }
                public struct S : IDisposable, IMutable
                {
                    public int Count;
                    private static int shared;
                    public ref int GetPinnableReference() { Count++; return ref shared; }
                    public void Mutate() { Count++; }
                    public int Property { get { return ++Count; } }
                    public int Mixed { get { Count++; return 0; } readonly set { } }
                    public E GetEnumerator() { Count++; return default; }
                    public E GetAsyncEnumerator() { Count++; return default; }
                    public void Dispose() { Count++; }
                    public Task DisposeAsync() { Count++; return Task.CompletedTask; }
                    public override string ToString() { Count++; return ""; }
                    public void Deconstruct(out int x, out int y) { Count++; x = y = 0; }
                    public TaskAwaiter GetAwaiter() { Count++; return Task.CompletedTask.GetAwaiter(); }
                }
                public struct E
                {
                    public bool MoveNext() => false;
                    public int Current => 0;
                    public Task<bool> MoveNextAsync() => Task.FromResult(false);
                    public Task DisposeAsync() => Task.CompletedTask;
                }
                public struct K
                {
                    public int Mutations;
                    public int Length { get { Mutations++; return 0; } }
                    public E GetEnumerator() { Mutations += 10; return default; }
                }
                public struct ExplicitEnumerable : IEnumerable<int>
                {
                    public int Count;
                    IEnumerator<int> IEnumerable<int>.GetEnumerator()
                    {
                        Count++;
                        return new List<int>().GetEnumerator();
                    }
                    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
                }
                public interface IMutable { void Mutate(); }
                public class Generic<T> where T : IMutable
                {
                    public T Mutable;
                    public readonly T Readonly;
                    public void Run() { Mutable?.Mutate(); Readonly?.Mutate(); }
                }
                public class Unconstrained<T>
                {
                    public T Mutable;
                    public readonly T Readonly;
                    public void Run() { Mutable?.ToString(); Readonly?.ToString(); }
                }
                """, new CSharpParseOptions(LanguageVersion.CSharp14))],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();
        var emitted = compilation.Emit(output);
        if (!emitted.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));
        var type = Assembly.Load(output.ToArray()).GetType("Probe");
        int Run(string name) => (int)type.GetMethod(name).Invoke(Activator.CreateInstance(type), null);
        Task<int> RunAsync(string name) => (Task<int>)type.GetMethod(name).Invoke(Activator.CreateInstance(type), null);
        await Assert.That(Run("Foreach")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyForeach")).IsEqualTo(0);
        await Assert.That(Run("InterfaceForeach")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyInterfaceForeach")).IsEqualTo(0);
        await Assert.That(Run("NullableForeach")).IsEqualTo(0);
        await Assert.That(Run("GenericConditional")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyGenericConditional")).IsEqualTo(0);
        await Assert.That(Run("UnconstrainedConditional")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyUnconstrainedConditional")).IsEqualTo(0);
        await Assert.That(await RunAsync("AsyncForeach")).IsEqualTo(1);
        await Assert.That(await RunAsync("ReadonlyAsyncForeach")).IsEqualTo(0);
        await Assert.That(Run("Using")).IsEqualTo(0);
        await Assert.That(await RunAsync("AsyncUsing")).IsEqualTo(0);
        await Assert.That(Run("Delegate")).IsEqualTo(0);
        await Assert.That(Run("Interpolation")).IsEqualTo(0);
        await Assert.That(Run("Deconstruct")).IsEqualTo(0);
        await Assert.That(Run("ReadonlyDeconstruct")).IsEqualTo(0);
        await Assert.That(await RunAsync("Await")).IsEqualTo(1);
        await Assert.That(await RunAsync("ReadonlyAwait")).IsEqualTo(0);
        await Assert.That(Run("Pattern")).IsEqualTo(0);
        await Assert.That(Run("ReadonlyPattern")).IsEqualTo(0);
        await Assert.That(Run("ReadonlySetterCompound")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyFieldSetterCompound")).IsEqualTo(0);
        await Assert.That(Run("Spread")).IsEqualTo(1);
        await Assert.That(Run("ReadonlySpread")).IsEqualTo(0);
        await Assert.That(Run("CountableSpread")).IsEqualTo(0);
        await Assert.That(Run("ThreeTemporarySpread")).IsEqualTo(0);
        await Assert.That(Run("FourTemporarySpread")).IsEqualTo(10);
        await Assert.That(Run("Fixed")).IsEqualTo(1);
        await Assert.That(Run("ReadonlyFixed")).IsEqualTo(0);
    }
}
