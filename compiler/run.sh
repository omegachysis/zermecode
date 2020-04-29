set -e

echo "Cleaning files..."
rm -rf bin/compiler || true
mkdir bin/compiler -p

echo "Generating compiler..."
bison grammar.y -d -o bin/compiler/compiler.c
flex -o bin/compiler/lex.c grammar.l

echo "Compiling compiler..."
g++ bin/compiler/compiler.c bin/compiler/lex.c -o bin/compiler/compiler -lm

echo
echo "Running compiler on input.txt..."
rm -rf bin/output || true
mkdir bin/output -p
./bin/compiler/compiler < input.txt > bin/output/output.c
echo

echo "Compiling intermediate program..."
g++ bin/output/output.c -o bin/output/output

echo "Running result..."
./bin/output/output