function list_child_processes () {
    local ppid=$1;
    local current_children=$(pgrep -P $ppid);
    local local_child;
    if [ $? -eq 0 ];
    then
        for current_child in $current_children
        do
          local_child=$current_child;
          list_child_processes $local_child;
          echo $local_child;
        done;
    else
      return 0;
    fi;
}

ps 81615;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 81615 > /dev/null;
done;

for child in $(list_child_processes 81642);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net6.0/c66b5ce76f50429a9cb1584f3d197ef9.sh;
