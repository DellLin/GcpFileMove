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

ps 87024;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 87024 > /dev/null;
done;

for child in $(list_child_processes 87035);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/94e75669ec8340eca24df721ce19c2cd.sh;
