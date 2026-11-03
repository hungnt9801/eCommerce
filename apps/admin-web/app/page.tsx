import { redirect } from "next/navigation";
import { DEFAULT_BUSINESS } from "@/lib/config";

export default function HomePage() {
  redirect(`/${DEFAULT_BUSINESS}`);
}
