set -e
dotnet run

echo
echo "Compiling..."
echo
g++ bin/ir.cpp -o bin/result

echo
echo "Running..."
echo
./bin/result