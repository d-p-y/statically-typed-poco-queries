cd StaTypPocoQueries.Core
dotnet pack StaTypPocoQueries.Core.fsproj -c Release
cd ..

cd StaTypPocoQueries.AsyncPoco 
dotnet pack StaTypPocoQueries.AsyncPoco.csproj -c Release
cd ..

cd StaTypPocoQueries.AsyncPocoDpy
dotnet pack StaTypPocoQueries.AsyncPocoDpy.csproj -c Release
cd ..

echo "exiting"
