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

ps 83847;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 83847 > /dev/null;
done;

for child in $(list_child_processes 83849);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net6.0/5da8983e1064417c896740f46938acad.sh;
