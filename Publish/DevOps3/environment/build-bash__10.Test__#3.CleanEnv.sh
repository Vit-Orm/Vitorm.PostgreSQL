set -e


#---------------------------------------------------------------------
# args

args_="

export basePath=/root/temp

# "


#---------------------------------------------------------------------
echo '#build-bash__10.Test_#3.CleanEnv.sh'


echo '#build-bash__10.Test_#3.CleanEnv.sh -> #1 remove PostgreSQL'
docker rm vitorm-postgres -f || true



