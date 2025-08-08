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

ps 90827;
while [ $? -eq 0 ];
do
  sleep 1;
  ps 90827 > /dev/null;
done;

for child in $(list_child_processes 90844);
do
  echo killing $child;
  kill -s KILL $child;
done;
rm /Users/delllin/Projects/GcpFileMove/bin/Debug/net8.0/fbfb37c26f89403587417007881c55e0.sh;
