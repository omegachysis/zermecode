set -e
dotnet run
g++ bin/ir.c -o bin/result
./bin/result