
# Hello world program
```
static type Program
{
    static fn Main()
    {
        Console.WriteLine("Hello, world!");
    }
}
```

# Basic function
```
fn square(Int x) -> Int
{
    return x * x;
}
```

# Mutable and immutable variable initialization
```
fn Foo()
{
    let x = 0; // Immutable
    let y := 0; // Mutable
    y := 1;
}
```

# Variable redefinition
```
fn Foo()
{
    let x = 0; // Immutable
    let x := 0; // Redefinition as mutable
}
```

# If/Else control flow
```
fn Foo()
{
    let x := 0;

    if x = 0 then Console.WriteLine(0);

    if x = 1 {
        Console.WriteLine(1);
    }

    if x = 2 {
        Console.WriteLine(2);
    }
    else {
        Console.WriteLine(3);
    }

    if x = 3 then 
        Console.WriteLine(4);
    else if x = 4 then 
        Console.WriteLine(5);
    else 
        Console.WriteLine(6);
}
```

# Unless control flow
```
fn Foo()
{
    let x := 0;

    unless x = 0 {
        Console.WriteLine(0);
    }

    unless x = 0 do
        Console.WriteLine(0);
}
```

# While/Do-While
```
fn Foo()
{
    let x := 0;

    while x != 1 {
        Console.WriteLine(x);
        x += 1;
    }

    do {
        Console.WriteLine(x);
        x += 1;
    } while x != 3;
}
```

# Until/Do-Until
```
fn Foo()
{
    let x := 0;

    until x = 1 {
        Console.WriteLine(x);
        x += 1;
    }

    do {
        Console.WriteLine(x);
        x += 1;
    } until x = 3;
}
```

# For loop
```
fn Foo()
{
    // Prints numbers 0 through 10 inclusively.
    for x in range[0, 10] {
        Console.WriteLine(x);
    }

    // Prints numbers 0 through 10 excluding ten.
    for x in range[0, 10) {
        Console.WriteLine(x);
    }

    // Prints numbers 0 through 10 inclusively in reverse.
    for x in range[10, 0] {
        Console.WriteLine(x);
    }

    // Prints even numbers -100 through 100 inclusively.
    for x in range[-100, 100] where x.isEven {
        Console.WriteLine(x);
    }

    // No braces.
    for x in range[0, 10] do 
        Console.WriteLine(x);
}
```

# List comprehension
```
fn Foo()
{
    // Store a list of every even number 0 to 100.
    let result = List(each x in range[0, 100] where x.isEven);
}
```

# Block expressions
```
fn Foo()
{
    let y = 0;

    // If/else expression.
    let x = 
        1 if y = 0 
        else 2;

    // Unless/then expression.
    let x = 1
        unless y = 0 
        then 2;

    // Ternary expression using block expressions.
    let x = 
        if y = 0 then 1 
        else 2;

    // Ternary expression using imperative block expressions.
    let x = 
        if y = 0 {
            let f = range[0, 100].Sum();
            f ** 2
        }
        else {
            let f = range[-100, 0].Sum();
            f ** 2
        }
}
```

# Collection initializers
```
fn Foo()
{
    let list = List[1, 2, 3];
    let list2 = List<Rat>[1, 2, 3]; // Casts the integers to rationals.
}
```

# Immutable borrowing is the default
```
fn BadIncrement(Int x)
{
    x += 1; // Compile error: 'x' was borrowed immutably.
}

fn Increment(Int& x)
{
    x += 1;
}

fn BadLeakage(Int x)
{
    let badList = List[1, 2, x]; // Compile error:
    // List[...] cannot take ownership of borrowed 'x'.
}

fn ListInitByCopy(Int x)
{
    let list = List[1, 2, x.copy()];
}

fn BadListAdd(List<Int> list, Int x)
{
    list.Add(x); // Compile error:
    // List.Add(...) cannot take ownership of borrowed 'x'.
}

fn ListAddByCopy(List<Int> list, Int x)
{
    list.Add(x.dynCopy());
}
```

# Immutable borrows are secure
```
fn Foo(Int x)
{
    Bar(x); // Compile error:
    // Bar(...) cannot borrow 'x' mutably because it is borrowed immutably.
}

fn Bar(Int& x) {}
```

# Borrows conform with mutable/immutable initialization
```
fn Foo()
{
    x = 0; // Immutable initialization.
    Bar(x); // Compile error:
    // Bar(...) cannot borrow 'x' mutably because it is immutable.
}

fn Bar(Int& x) {}
```

# Dynamic memory
```
fn Foo()
{
    let immutableHeapAllocatedInt = dyn 1;
    let mutableHeapAllocatedInt := dyn 2;

    Borrower(immutableHeapAllocatedInt);
    // immutableHeapAllocatedInt is stil available.

    MutBorrower(immutableHeapAllocatedInt); // Compile error:
    // MutBorrower(...) cannot borrow 'immutableHeapAllocatedInt' mutably 
    // it is immutable.

    MutBorrower(mutableHeapAllocatedInt);
    // mutableHeapAllocatedInt is stil available.

    Transferrer(immutableHeapAllocatedInt);
    // immutableHeapAllocatedInt is no longer available.

    MutTransferrer(mutableHeapAllocatedInt);
    // mutableHeapAllocatedInt is no longer available.
}

fn Borrower(Int x) {}

fn MutBorrower(Int& x) {}

fn Transferrer(Int* x) {}

fn MutTransferrer(Int*& x) {}
```

# Basic user-defined type
```
type Foo
{
    // Value members (copy semantics from constructor):
    Int _privateImmutableMember = 1;
    Int PublicMutableMember := 2;
    Int ImmutableRequiredOnConstruction = ?;
    Int MutableRequiredOnConstruction := ?;
    Int _mutableOptionalOnConstruction := 1?;

    // Dynamic members (heap-allocated memory transferred by constructor):
    Int* DynamicMember := ?;
}

fn Test()
{
    let heapAllocatedInt = dyn 3;

    let foo = Foo(
        ImmutableRequiredOnConstruction: 1,
        MutableRequiredOnConstruction: 2,
        DynamicMember: heapAllocatedInt,
    );

    // heapAllocatedInt is transferred to Foo, it is no longer available here.
    Console.Write(heapAllocatedInt); // Compile error:
    // Console.Write(...) cannot borrow 'heapAllocatedInt' because it was transferred 
    // into 'Foo(...)' on line 278.
}
```

# References
```
fn Foo()
{
    let x = 0;
    let refToX = x';
    Console.Write(x'); // "0"
    
    let y := 1;
    let refToY = y';
    let mutRefToY = y&;
    Console.Write(y'); // "1";

    let list = List["1", "2"];
    let listMut = ListMut["3", "4"];

    Console.Write(First(list)); // "1";

    First(list).append("x"); // Compile error:
    // Cannot invoke mutable method 'append(...)' on an immutable reference 
    // to String.

    Console.Write(First(listMut)); // Compile error:

}

fn First(List<String> strings) -> String'
{
    return strings[0];
}

fn First&(ListMut<String> strings) -> String&
{
    return strings[0];
}
```