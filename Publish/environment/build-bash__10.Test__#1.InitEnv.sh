set -e


#---------------------------------------------------------------------
# args

args_="

export basePath=/root/temp

# "


#---------------------------------------------------------------------
echo '#build-bash__10.Test__#1.InitEnv.sh -> #1 start PostgreSQL container'
docker rm vitorm-postgres -f || true
docker run -d --name vitorm-postgres -p 5432:5432 -e POSTGRES_PASSWORD=123456 -e POSTGRES_DB=db_orm -e ALLOW_IP_RANGE=0.0.0.0/0 postgres:15.8


#---------------------------------------------------------------------
echo '#build-bash__10.Test__#1.InitEnv.sh -> #8 wait for containers to init'


echo '#build-bash__10.Test__#1.InitEnv.sh -> #8.1 wait for PostgreSQL to init' 
docker run -t --rm --link vitorm-postgres postgres:15.8 timeout 120 sh -c "export PGPASSWORD=123456; until psql -h vitorm-postgres -U postgres -d db_orm -c 'create database db_orm2;'; do echo waiting for PostgreSQL; sleep 2; done;"


#---------------------------------------------------------------------
echo '#build-bash__10.Test__#1.InitEnv.sh -> #9 init test environment success!'