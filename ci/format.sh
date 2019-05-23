cd $(dirname "${BASH_SOURCE[0]}")

docker build . -f ./Dockerfile -t online-services-ci:latest

cd ../
docker run -v $(pwd):/code online-services-ci:latest  /bin/bash  -c "dotnet format -w /code/services/csharp --dry-run --check"
