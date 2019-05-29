cd $(dirname "${BASH_SOURCE[0]}")

docker build docker/online-services-ci -f ./docker/online-services-ci/Dockerfile -t online-services-ci:latest

cd ../
docker run -e LOCAL_USER_ID=$(id -u) -v $(pwd):/code online-services-ci:latest /bin/bash -c "dotnet format -w /code/services/csharp --check"
