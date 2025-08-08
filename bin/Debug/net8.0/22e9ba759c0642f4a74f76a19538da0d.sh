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

ps 87913;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 87913 > /dev/null;
done;

for child in $(list_child_processes 87937);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/22e9ba759c0642f4a74f76a19538da0d.sh;
