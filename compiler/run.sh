echo "Generating compiler..."
bison grammar.y -o compiler.c

echo "Compiling compiler..."
g++ compiler.c -o compiler.out

echo "Running compiler..."
./compiler.out