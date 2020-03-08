set -e
dotnet run

echo
echo "Compiling..."
echo
g++ bin/ir.cpp -o bin/result -lgmp -lgmpxx

echo
echo "Running..."
echo
./bin/result