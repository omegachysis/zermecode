set -e
dotnet run

echo
echo "Compiling..."
echo
g++-9 -Wall bin/ir.cpp -o bin/result -lgmp -lgmpxx -lm

echo
echo "Running..."
echo
./bin/result