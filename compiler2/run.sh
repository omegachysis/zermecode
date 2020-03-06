set -e
dotnet run
g++ bin/ir.cpp -o bin/result
./bin/result